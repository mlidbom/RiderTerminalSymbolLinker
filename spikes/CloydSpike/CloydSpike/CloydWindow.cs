using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ClaudeTooling.SessionHost.Protocol;
using CloydSpike.Chat;
using CloydSpike.Hub;
using CloydSpike.SessionHosts;
using CloydSpike.TuiHosts;

namespace CloydSpike;

/// <summary>
/// The spike's window: the sessions of its project, or of every project; the chosen one's conversation, with the input box
/// under it; and, beside it, its TUI in the chosen candidate host, for a session host the spike started.
/// </summary>
sealed class CloydWindow : Window
{
   readonly TheHub _hub = TheHub.Announced();
   readonly DirectoryInfo? _project;
   readonly ComboBox _sessions = new() { MinWidth = 360 };
   readonly ComboBox _tuiHosts = new() { ItemsSource = TuiHost.Candidates, SelectedIndex = 0, MinWidth = 220 };
   readonly ConversationView _conversation = new();
   readonly MessageBox _messageBox = new();
   readonly TextBlock _status = new() { Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
   readonly ContentControl _tui = new();
   AttachmentToASessionHost? _attachment;
   IReadOnlyList<SessionHostOfTheHub> _sessionHosts = [];

   public CloydWindow(DirectoryInfo? project, string? tuiToShow)
   {
      _project = project;
      Title = project is null ? "Cloyd spike: every session" : $"Cloyd spike: {project.Name}";
      Width = 1600;
      Height = 950;
      ShowActivated = tuiToShow is null;
      Content = Layout();
      _sessions.SelectionChanged += (_, _) => FollowTheChosenSession();
      _messageBox.Sending += text => _ = Send(text);
      Opened += async (_, _) =>
      {
         await RefreshTheSessions();
         if(tuiToShow is null) return;

         _tuiHosts.SelectedItem = TuiHost.Candidates.First(it => it.Name.StartsWith(tuiToShow, StringComparison.OrdinalIgnoreCase));
         await ShowTheTui();
      };
      Closed += (_, _) => _attachment?.Dispose();
   }

   RunningSession? ChosenSession => _sessions.SelectedItem as RunningSession;

   Control Layout()
   {
      var bar = new StackPanel
      {
         Orientation = Orientation.Horizontal,
         Spacing = 8,
         Margin = new Thickness(8),
         Children =
         {
            _sessions,
            Button("Refresh", RefreshTheSessions),
            Button("Start a throwaway session", StartAThrowawaySession),
            new TextBlock { Text = "TUI in:", VerticalAlignment = VerticalAlignment.Center },
            _tuiHosts,
            Button("Show the TUI", ShowTheTui),
            Button("…in Windows Terminal", ShowTheTuiInWindowsTerminal),
            Button("End the throwaway session", EndTheThrowawaySession)
         }
      };
      var chat = new DockPanel { Margin = new Thickness(8, 0, 4, 8) };
      DockPanel.SetDock(_messageBox, Dock.Bottom);
      DockPanel.SetDock(_status, Dock.Bottom);
      chat.Children.Add(_messageBox);
      chat.Children.Add(_status);
      chat.Children.Add(_conversation);

      var panes = new Grid { ColumnDefinitions = new ColumnDefinitions("*,4,*") };
      Grid.SetColumn(chat, 0);
      var splitter = new GridSplitter { Background = Brushes.DimGray };
      Grid.SetColumn(splitter, 1);
      var tui = new Border { Margin = new Thickness(4, 0, 8, 8), Background = Brushes.Black, Child = _tui };
      Grid.SetColumn(tui, 2);
      panes.Children.Add(chat);
      panes.Children.Add(splitter);
      panes.Children.Add(tui);

      var window = new DockPanel();
      DockPanel.SetDock(bar, Dock.Top);
      window.Children.Add(bar);
      window.Children.Add(panes);
      return window;
   }

   static Button Button(string text, Func<Task> click)
   {
      var button = new Button { Content = text };
      button.Click += async (_, _) => await click();
      return button;
   }

   async Task RefreshTheSessions()
   {
      var chosen = ChosenSession?.SessionId;
      var sessions = (await _hub.RunningSessions()).Where(it => _project is null || it.Directory.StartsWith(_project.FullName, StringComparison.OrdinalIgnoreCase)).ToList();
      _sessionHosts = await _hub.SessionHosts();
      _sessions.ItemsSource = sessions;
      _sessions.SelectedItem = sessions.FirstOrDefault(it => it.SessionId == chosen) ?? sessions.FirstOrDefault(it => it.Name == ThrowawaySession.Name) ?? sessions.FirstOrDefault();
   }

   void FollowTheChosenSession()
   {
      if(ChosenSession is { } session) _conversation.Follow(_hub, session);
   }

   async Task Send(string text)
   {
      if(ChosenSession is not { } session) return;

      _status.Text = "Sending…";
      var whyNot = await _hub.Send(session, text);
      _status.Text = whyNot ?? "";
      if(whyNot is null) _messageBox.Sent();
   }

   async Task StartAThrowawaySession()
   {
      _status.Text = "Starting a session host, and Claude Code in it…";
      await ThrowawaySession.Start(_hub);
      for(var tries = 0; tries < 30 && !(await _hub.RunningSessions()).Any(it => it.Name == ThrowawaySession.Name); tries++) await Task.Delay(1000);
      await RefreshTheSessions();
      _status.Text = "";
   }

   SessionHostOfTheHub? HostOfTheChosenSession => _sessionHosts.FirstOrDefault(it => it.Id == ChosenSession?.SessionHost);

   Task ShowTheTui()
   {
      if(HostOfTheChosenSession is not { IsTheSpikes: true } host)
      {
         _status.Text = "The spike shows the TUI only of a session host it started: attaching takes the session over from its tab.";
         return Task.CompletedTask;
      }

      _attachment?.Dispose();
      _attachment = AttachmentToASessionHost.To(new SessionHostId(host.Id), ConsoleSize.Default);
      _attachment.Ended += why => Dispatcher.UIThread.Post(() => _status.Text = $"The TUI was let go: {why}.");
      _tui.Content = ((TuiHost)_tuiHosts.SelectedItem!).Showing(_attachment);
      Dispatcher.UIThread.Post(() => (_tui.Content as Control)?.Focus(), DispatcherPriority.Background);
      return Task.CompletedTask;
   }

   Task ShowTheTuiInWindowsTerminal()
   {
      if(HostOfTheChosenSession is not { IsTheSpikes: true } host) return Task.CompletedTask;

      Process.Start(new ProcessStartInfo("wt.exe", ["-w", "new", "--title", $"Cloyd spike: {host.Id}", .. host.AttachCommand]) { UseShellExecute = false });
      return Task.CompletedTask;
   }

   async Task EndTheThrowawaySession()
   {
      foreach(var host in (await _hub.SessionHosts()).Where(it => it.IsTheSpikes)) await _hub.End(host);
      _tui.Content = null;
      await RefreshTheSessions();
   }
}
