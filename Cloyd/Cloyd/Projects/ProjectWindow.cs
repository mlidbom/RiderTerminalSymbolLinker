using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Hub;
using Cloyd.SessionHosts;
using Cloyd.Sessions;

namespace Cloyd.Projects;

/// <summary>
/// The window of one <see cref="Projects.Project"/>: a <see cref="SessionTab"/> for each of its session hosts, each
/// showing the host's terminal, and the <see cref="NewSessionButton"/>. Closing a tab ends its session. The sessions
/// outlive the window: closing it only lets go of their hosts, which the project's next window shows again.
/// </summary>
/// <remarks>
/// As it opens, it shows each of the project's hosts that no attach client shows: the ones its earlier windows started,
/// and the ones Claude Sessions' tabs ran in, once no IDE's tab shows them. A host that an IDE's tab shows is left to the
/// tab, since attaching would take the session over from it.
/// </remarks>
sealed class ProjectWindow : Window
{
   static readonly TimeSpan BeforeFollowingTheRunningSessionsAgain = TimeSpan.FromSeconds(1);

   readonly Project _project;
   readonly TheHub _hub;
   readonly ShellOfNewSessions _shellOfNewSessions;
   readonly TabControl _tabs = new();
   readonly NewSessionButton _newSession = new();
   readonly TextBlock _failure = new() { IsVisible = false, Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 4) };
   readonly CancellationTokenSource _closed = new();

   public ProjectWindow(Project project, TheHub hub, ShellOfNewSessions shellOfNewSessions)
   {
      _project = project;
      _hub = hub;
      _shellOfNewSessions = shellOfNewSessions;
      Title = $"Cloyd: {project.Name}";
      Width = 1400;
      Height = 900;
      Content = TheLayout();
      _newSession.NewSessionAsked += name => _ = TellingOfFailure(() => StartANewSession(name));
      _tabs.SelectionChanged += (_, _) => Dispatcher.UIThread.Post(() => SelectedTab?.Terminal.Focus(), DispatcherPriority.Loaded);
   }

   public IReadOnlyList<SessionTab> Tabs => _tabs.Items.OfType<SessionTab>().ToList();

   public SessionTab? SelectedTab => _tabs.SelectedItem as SessionTab;

   /// <summary>The last failure the window told of, at its foot: null while there was none.</summary>
   public Exception? Failure { get; private set; }

   /// <summary>Showing the hosts that ran as the window opened: done once each has its tab, or the window has told why not.</summary>
   public Task ShowingTheSessionHostsThatRan { get; private set; } = Task.CompletedTask;

   protected override void OnOpened(EventArgs e)
   {
      base.OnOpened(e);
      ShowingTheSessionHostsThatRan = TellingOfFailure(ShowTheSessionHostsThatRun);
      _ = FollowTheRunningSessions();
   }

   protected override void OnClosed(EventArgs e)
   {
      _closed.Cancel();
      foreach(var tab in Tabs) tab.LetGo();
      base.OnClosed(e);
   }

   /// <summary>
   /// Starts a new session in the project's folder, in a tab of its own, which it selects: named
   /// <paramref name="name"/>, or with no name when it is null. When the hub doesn't start Claude Code in it, the tab
   /// says why, and shows the shell at its prompt.
   /// </summary>
   public async Task StartANewSession(string? name)
   {
      var host = await _hub.StartASessionHost(_shellOfNewSessions, _project.Folder, SessionHostLabel.OfANewHostOf(_project), _closed.Token);
      var tab = Show(host, name);
      _tabs.SelectedItem = tab;
      var command = ClaudeCodeCommand.OfANewSession(name);
      try
      {
         await _hub.StartClaudeCodeIn(host, command, _closed.Token);
      }
      catch(RefusedByTheHub refusal)
      {
         tab.Tell($"The hub didn't type {command}: {refusal.Message}");
      }
   }

   async Task ShowTheSessionHostsThatRun()
   {
      foreach(var host in await _hub.SessionHosts(_closed.Token))
         if(_project.Equals(host.Project) && !host.Attached)
            Show(host, nameGiven: null);
      if(_tabs.Items.Count > 0) _tabs.SelectedIndex = 0;
   }

   SessionTab Show(SessionHostOfTheHub host, string? nameGiven)
   {
      var tab = new SessionTab(host, nameGiven, SizeOfTheTerminals);
      tab.ClosedByMagnus += it => _ = TellingOfFailure(() => EndTheSessionOf(it));
      tab.EndedByItself += (it, _) => Remove(it);
      _tabs.Items.Add(tab);
      return tab;
   }

   /// <summary>Ends the session of <paramref name="tab"/>, as Magnus asked by closing it: whether or not the window closes meanwhile.</summary>
   async Task EndTheSessionOf(SessionTab tab)
   {
      Remove(tab);
      await _hub.End(tab.Host.Id, CancellationToken.None);
   }

   void Remove(SessionTab tab)
   {
      tab.LetGo();
      _tabs.Items.Remove(tab);
   }

   /// <summary>The size of the terminals shown, which a new tab's host is attached at: the default size of a host's console before any is shown.</summary>
   ConsoleSize SizeOfTheTerminals => SelectedTab?.Terminal.Size is { IsASize: true } size ? size : ConsoleSize.Default;

   /// <summary>Tells each tab which Claude Code runs in its host, each time the hub's list of them changes, until the window closes.</summary>
   async Task FollowTheRunningSessions()
   {
      while(!_closed.IsCancellationRequested)
      {
         await TellingOfFailure(async () =>
         {
            await foreach(var runningSessions in _hub.RunningSessionsAsTheyChange(_closed.Token))
               foreach(var tab in Tabs)
                  tab.SessionRunningInIt = runningSessions.FirstOrDefault(it => it.SessionHost == tab.Host.Id);
         });
         await Task.Delay(BeforeFollowingTheRunningSessionsAgain, CancellationToken.None);
      }
   }

   /// <summary>
   /// Does <paramref name="doing"/>, which the window does on its own or at Magnus's click, and tells its failure at the
   /// window's foot, as nothing else waits for it.
   /// </summary>
   async Task TellingOfFailure(Func<Task> doing)
   {
      try
      {
         await doing();
      }
      catch(OperationCanceledException) when(_closed.IsCancellationRequested)
      {
         // The window closed meanwhile: nothing is left to tell.
      }
      catch(Exception failure)
      {
         Failure = failure;
         _failure.Text = failure.Message;
         _failure.IsVisible = true;
      }
   }

   Control TheLayout()
   {
      var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4), Children = { _newSession } };
      DockPanel.SetDock(bar, Dock.Top);
      DockPanel.SetDock(_failure, Dock.Bottom);
      return new DockPanel { Children = { bar, _failure, _tabs } };
   }
}
