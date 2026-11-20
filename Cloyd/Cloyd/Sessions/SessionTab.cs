using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.ClaudeCode;
using Cloyd.Hub;
using Cloyd.Saving;
using Cloyd.SessionHosts;
using Cloyd.Terminals;

namespace Cloyd.Sessions;

/// <summary>
/// A tab of a <see cref="Projects.ProjectWindow"/>: the terminal of one session host, showing its shell, and the Claude
/// Code in the shell as Claude Code draws its TUI. Titled with the name of the session that runs in it, or ran in it
/// last.
/// </summary>
sealed class SessionTab : TabItem
{
   const string TitleOfASessionWithNoName = "Claude";

   readonly AttachmentToASessionHost _attachment;
   readonly TextBlock _title;
   readonly TextBlock _notice = new() { IsVisible = false, Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 4) };

   /// <summary>
   /// Attaches to <paramref name="host"/> at <paramref name="size"/>, which its terminal then resizes it from. Until the
   /// hub tells of the session that runs in it, its <see cref="LatestSession"/> is <paramref name="latestSession"/>, as a
   /// saved tab's, and it is titled with that session's name, or <paramref name="nameGiven"/>, as a new session was named.
   /// </summary>
   public SessionTab(SessionHostOfTheHub host, ClaudeSession? latestSession, string? nameGiven, ConsoleSize size)
   {
      Host = host;
      LatestSession = latestSession;
      _attachment = AttachmentToASessionHost.To(host.Id, size);
      _attachment.Ended += why => Dispatcher.UIThread.Post(() => EndedByItself?.Invoke(this, why));
      Terminal = new TerminalOfASessionHost(_attachment);
      _title = new TextBlock { Text = latestSession?.Name ?? nameGiven ?? TitleOfASessionWithNoName, VerticalAlignment = VerticalAlignment.Center };
      Header = TheHeader();
      Content = new DockPanel { Children = { Docked(_notice, Dock.Top), Terminal } };
   }

   protected override Type StyleKeyOverride => typeof(TabItem);

   public SessionHostOfTheHub Host { get; }

   public TerminalOfASessionHost Terminal { get; }

   /// <summary>The tab's title: the name of the session that runs in its host, or ran in it last.</summary>
   public string Title => _title.Text!;

   /// <summary>What the tab tells above its terminal, as why the hub didn't start Claude Code in it: null while it tells nothing.</summary>
   public string? Notice => _notice.IsVisible ? _notice.Text : null;

   /// <summary>Raised when Magnus clicks the tab's ×, which is to end its session.</summary>
   public event Action<SessionTab>? ClosedByMagnus;

   /// <summary>Raised on the UI thread, with why, once the host has let the tab go: its shell exited, or another attach client took the session over.</summary>
   public event Action<SessionTab, string>? EndedByItself;

   /// <summary>The Claude Code that runs in the tab's host, as the hub last told: null while none runs, as before it starts, or after it exits to the shell.</summary>
   public RunningSession? SessionRunningInIt
   {
      get;
      set
      {
         field = value;
         if(value is null) return;
         LatestSession = value.Session;
         _title.Text = value.Name ?? TitleOfASessionWithNoName;
      }
   }

   /// <summary>
   /// The session that runs in the tab's host, or ran in it last, as after Claude Code exits to the shell: what the tab
   /// is saved with, to be resumed once its host is gone. Null while none has, as before Claude Code starts.
   /// </summary>
   public ClaudeSession? LatestSession { get; private set; }

   /// <summary>Whether the Claude Code that runs in the tab's host runs as <paramref name="process"/>.</summary>
   public bool RunsTheClaudeCodeOf(ClaudeCodeProcessId process) => SessionRunningInIt?.Process == process;

   /// <summary>Whether <paramref name="session"/> runs in the tab's host, or ran in it last: its <see cref="LatestSession"/>.</summary>
   public bool RunsOrRanLast(ClaudeSessionId session) => LatestSession?.Id == session;

   /// <summary>The tab, as its window saves it.</summary>
   public SavedSessionTab AsSaved => new(Host.Id, Host.Directory, LatestSession);

   /// <summary>Shows <paramref name="notice"/> above the terminal.</summary>
   public void Tell(string notice)
   {
      _notice.Text = notice;
      _notice.IsVisible = true;
   }

   /// <summary>Lets go of the tab's host, whose session runs on.</summary>
   public void LetGo() => _attachment.Dispose();

   Control TheHeader()
   {
      var close = new Button { Content = "×", Padding = new Thickness(6, 0), Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center };
      ToolTip.SetTip(close, "End the session");
      close.Click += (_, _) => ClosedByMagnus?.Invoke(this);
      return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { _title, close } };
   }

   static Control Docked(Control control, Dock dock)
   {
      DockPanel.SetDock(control, dock);
      return control;
   }
}
