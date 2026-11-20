using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.ClaudeCode;
using Cloyd.DotNetAnalysisAndRefactoring;
using Cloyd.Hub;
using Cloyd.Ides;
using Cloyd.Links;
using Cloyd.Saving;
using Cloyd.SessionHosts;
using Cloyd.Sessions;
using Cloyd.VirtualDesktops;

namespace Cloyd.Projects;

/// <summary>
/// The window of one <see cref="Projects.Project"/>: a <see cref="SessionTab"/> for each of its session hosts, each
/// showing the host's terminal, and the <see cref="NewSessionButton"/>. Closing a tab ends its session. The sessions
/// outlive the window: closing it only lets go of their hosts, which the project's next window shows again.
/// </summary>
/// <remarks>
/// It saves its tabs, in their order, and which is selected, every <see cref="BetweenSaves"/> and as it closes, in
/// <see cref="SavedProjectWindows"/>. As it opens, it brings them back: a tab whose host runs is attached to it again,
/// and a tab whose host is gone, as after a reboot, has its session resumed in a new host. Then it shows each of the
/// project's other hosts that no attach client shows: the ones Claude Sessions' tabs ran in, once no IDE's tab shows
/// them, and the ones the window started and didn't get to save. A host that an IDE's tab shows is left to the tab,
/// since attaching would take the session over from it.
/// </remarks>
/// <remarks>
/// It keeps to the virtual desktop of the project's window in an IDE (<see cref="DesktopOfItsIdesWindow"/>): Magnus keeps
/// each IDE window on a desktop of its own, and Cloyd's window beside it.
/// </remarks>
sealed class ProjectWindow : Window
{
   static readonly TimeSpan BeforeFollowingTheRunningSessionsAgain = TimeSpan.FromSeconds(1);
   static readonly TimeSpan BetweenSaves = TimeSpan.FromSeconds(2);
   static readonly TimeSpan BetweenLooksAtItsIdesWindow = TimeSpan.FromSeconds(2);
   static readonly TimeSpan BetweenReadingsOfItsFiles = TimeSpan.FromSeconds(5);

   /// <summary>How long the symbols the window gathered last serve: one gathering takes Compze's solution 6.4 s of Rider's time.</summary>
   static readonly TimeSpan BetweenGatheringsOfItsSymbols = TimeSpan.FromMinutes(2);

   /// <summary>How soon the window asks for the symbols again after Rider didn't answer, as while it still loads the solution, which it does as Cloyd's window opens.</summary>
   static readonly TimeSpan BeforeAskingAgainForItsSymbols = TimeSpan.FromSeconds(10);

   readonly TheHub _hub;
   readonly TheIdes _ides;
   readonly ShellOfNewSessions _shellOfNewSessions;
   readonly SavedProjectWindows _savedWindows;
   readonly TabControl _tabs = new();
   readonly NewSessionButton _newSession = new();
   readonly TextBlock _failure = new() { IsVisible = false, Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 4) };
   readonly CancellationTokenSource _closed = new();

   /// <summary>What saves the window every <see cref="BetweenSaves"/>: null until it has brought back the saved tabs, so that it never saves a window they aren't in yet.</summary>
   DispatcherTimer? _saving;

   public ProjectWindow(Project project, TheHub hub, TheIdes ides, DotNetAnalysisAndRefactoringMcp dotNetAnalysisAndRefactoringMcp, ShellOfNewSessions shellOfNewSessions, SavedProjectWindows savedWindows)
   {
      Project = project;
      FilesAndSymbols = new FilesAndSymbolsOfAProject(project, dotNetAnalysisAndRefactoringMcp);
      _hub = hub;
      _ides = ides;
      _shellOfNewSessions = shellOfNewSessions;
      _savedWindows = savedWindows;
      Title = $"Cloyd: {project.Name}";
      Width = 1400;
      Height = 900;
      Content = TheLayout();
      _newSession.NewSessionAsked += name => _ = TellingOfFailure(() => StartANewSession(name));
      _tabs.SelectionChanged += (_, _) => Dispatcher.UIThread.Post(() => SelectedTab?.Terminal.Focus(), DispatcherPriority.Loaded);
   }

   /// <summary>The project whose sessions the window shows.</summary>
   public Project Project { get; }

   /// <summary>The project's files and C# symbols, which what its chats show is to link to: the window keeps them current while it's open, from the first time something asks what links.</summary>
   public FilesAndSymbolsOfAProject FilesAndSymbols { get; }

   public IReadOnlyList<SessionTab> Tabs => _tabs.Items.OfType<SessionTab>().ToList();

   public SessionTab? SelectedTab => _tabs.SelectedItem as SessionTab;

   /// <summary>The last failure the window told of, at its foot: null while there was none.</summary>
   public Exception? Failure { get; private set; }

   /// <summary>
   /// Bringing back the tabs saved of the project's last window, and showing the project's other hosts, as the window
   /// opened: done once each has its tab, or the window has told why not.
   /// </summary>
   public Task BringingBackTheTabs { get; private set; } = Task.CompletedTask;

   /// <summary>
   /// The virtual desktop the project's window in an IDE was on as the window last looked, which it went to: null until an
   /// IDE showed the project.
   /// </summary>
   public VirtualDesktop? DesktopOfItsIdesWindow { get; private set; }

   /// <summary>
   /// Whether the window is on the virtual desktop shown, where Magnus is. A window of Avalonia's headless platform, which
   /// Windows doesn't know, is on no desktop but the one shown.
   /// </summary>
   public bool IsOnTheCurrentVirtualDesktop => NativeWindow.Of(this)?.IsOnTheCurrentVirtualDesktop ?? true;

   protected override void OnOpened(EventArgs e)
   {
      base.OnOpened(e);
      BringingBackTheTabs = TellingOfFailure(async () =>
      {
         await BringBackTheTabs();
         _saving = new DispatcherTimer(BetweenSaves, DispatcherPriority.Background, (_, _) => TellingOfFailure(Save));
      });
      _ = FollowTheRunningSessions();
      _ = FollowItsIdesWindow();
      _ = KeepItsFilesCurrent();
      _ = KeepItsSymbolsCurrent();
   }

   protected override void OnClosed(EventArgs e)
   {
      _closed.Cancel();
      if(_saving is not null)
      {
         _saving.Stop();
         TellingOfFailure(Save);
      }

      foreach(var tab in Tabs) tab.LetGo();
      base.OnClosed(e);
   }

   /// <summary>
   /// Brings the window forward: restored when it is minimized, and activated, in front of the others where Windows lets
   /// Cloyd (<see cref="LocalApi.TheForeground"/>). Windows then switches to its virtual desktop.
   /// </summary>
   public void BringForward()
   {
      if(WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
      Activate();
   }

   /// <summary>Selects <paramref name="tab"/>, one of the window's <see cref="Tabs"/>, which focuses its terminal.</summary>
   public void Select(SessionTab tab) => _tabs.SelectedItem = tab;

   /// <summary>
   /// Starts a new session in the project's folder, in a tab of its own, which it selects: named
   /// <paramref name="name"/>, or with no name when it is null. When the hub doesn't start Claude Code in it, the tab
   /// says why, and shows the shell at its prompt.
   /// </summary>
   public async Task StartANewSession(string? name)
   {
      var tab = await ShowANewHost(Project.Folder, latestSession: null, nameGiven: name);
      _tabs.SelectedItem = tab;
      await StartClaudeCodeIn(tab, ClaudeCodeCommand.OfANewSession(name));
   }

   /// <summary>
   /// Brings back the tabs the project's last window saved, in their order, then shows the project's other hosts that no
   /// attach client shows, and selects the tab that was selected: else the first.
   /// </summary>
   async Task BringBackTheTabs()
   {
      var saved = _savedWindows.Of(Project);
      var hosts = await _hub.SessionHosts(_closed.Token);
      var claudeCode = await _hub.ClaudeCodeFolderItServes(_closed.Token);
      SessionTab? selected = null;
      foreach(var savedTab in saved.Tabs)
         if(await BringBack(savedTab, hosts.SingleOrDefault(it => it.Id == savedTab.SessionHost), claudeCode) is {} tab && savedTab.SessionHost == saved.SelectedTab)
            selected = tab;

      foreach(var host in hosts.Where(it => Project.Equals(it.Project) && !it.Attached && !IsShown(it.Id)))
         Show(host, latestSession: null, nameGiven: null);
      _tabs.SelectedItem = selected ?? Tabs.FirstOrDefault();
   }

   /// <summary>
   /// Brings back <paramref name="saved"/>: attached to its <paramref name="host"/> while the host runs, unless an attach
   /// client shows it; once the host is gone, its session resumed in a new host, while <paramref name="claudeCode"/>
   /// holds the session's transcript. Null when the tab doesn't come back.
   /// </summary>
   async Task<SessionTab?> BringBack(SavedSessionTab saved, SessionHostOfTheHub? host, ClaudeCodeFolder claudeCode)
   {
      if(host is not null) return host.Attached ? null : Show(host, saved.Session, nameGiven: null);
      if(saved.Session is not {} session || !claudeCode.HoldsTheTranscriptOf(session.Id)) return null;

      var tab = await ShowANewHost(saved.Directory, session, nameGiven: null);
      await StartClaudeCodeIn(tab, ClaudeCodeCommand.Resuming(session));
      return tab;
   }

   async Task<SessionTab> ShowANewHost(string directory, ClaudeSession? latestSession, string? nameGiven) =>
      Show(await _hub.StartASessionHost(_shellOfNewSessions, directory, SessionHostLabel.OfANewHostOf(Project), _closed.Token), latestSession, nameGiven);

   /// <summary>Has the hub start <paramref name="command"/> in the host of <paramref name="tab"/>: when it doesn't, the tab says why, and shows the shell at its prompt.</summary>
   async Task StartClaudeCodeIn(SessionTab tab, ClaudeCodeCommand command)
   {
      try
      {
         await _hub.StartClaudeCodeIn(tab.Host, command, _closed.Token);
      }
      catch(RefusedByTheHub refusal)
      {
         tab.Tell($"The hub didn't type {command}: {refusal.Message}");
      }
   }

   SessionTab Show(SessionHostOfTheHub host, ClaudeSession? latestSession, string? nameGiven)
   {
      var tab = new SessionTab(host, latestSession, nameGiven, SizeOfTheTerminals, _hub);
      tab.ClosedByMagnus += it => _ = TellingOfFailure(() => EndTheSessionOf(it));
      tab.EndedByItself += (it, _) => Remove(it);
      _tabs.Items.Add(tab);
      return tab;
   }

   bool IsShown(SessionHostId host) => Tabs.Any(it => it.Host.Id == host);

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

   void Save() => _savedWindows.Save(Project, new SavedProjectWindow([.. Tabs.Select(it => it.AsSaved)], SelectedTab?.Host.Id));

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
   /// Keeps the window on the virtual desktop of the project's window in an IDE, until the window closes: moves it there
   /// once an IDE shows the project, and again each time that window is on another desktop, as when Magnus moves it, or
   /// the IDE restarts. A desktop Magnus moves Cloyd's window to himself it stays on until the IDE's window moves.
   /// </summary>
   async Task FollowItsIdesWindow()
   {
      while(!_closed.IsCancellationRequested)
      {
         await TellingOfFailure(async () =>
         {
            if((await _ides.WindowOf(Project, _closed.Token))?.Desktop is {} desktop && desktop != DesktopOfItsIdesWindow)
            {
               DesktopOfItsIdesWindow = desktop;
               NativeWindow.Of(this)?.MoveTo(desktop);
            }
         });
         await Task.Delay(BetweenLooksAtItsIdesWindow, CancellationToken.None);
      }
   }

   /// <summary>
   /// Reads the project's files again and again until the window closes, from the first time something asks what links,
   /// so that a file Claude Code just wrote links once it's named.
   /// </summary>
   async Task KeepItsFilesCurrent()
   {
      await FilesAndSymbols.FirstAskedFor.WaitAsync(_closed.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
      while(!_closed.IsCancellationRequested)
      {
         await TellingOfFailure(() => FilesAndSymbols.ReadTheFiles(_closed.Token));
         await Task.Delay(BetweenReadingsOfItsFiles, CancellationToken.None);
      }
   }

   /// <summary>
   /// Gathers the names of the project's C# types and members again and again until the window closes, from the first time
   /// something asks what links, so that those Claude Code adds link once it names them: soon again while Rider doesn't
   /// answer.
   /// </summary>
   async Task KeepItsSymbolsCurrent()
   {
      await FilesAndSymbols.FirstAskedFor.WaitAsync(_closed.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
      while(!_closed.IsCancellationRequested)
      {
         var riderAnswered = false;
         await TellingOfFailure(async () => riderAnswered = await FilesAndSymbols.GatherTheSymbols(_closed.Token));
         await Task.Delay(riderAnswered ? BetweenGatheringsOfItsSymbols : BeforeAskingAgainForItsSymbols, CancellationToken.None);
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

   /// <summary>Does <paramref name="doing"/> as <see cref="TellingOfFailure(Func{Task})"/> does, at once.</summary>
   void TellingOfFailure(Action doing) =>
      _ = TellingOfFailure(() =>
      {
         doing();
         return Task.CompletedTask;
      });

   Control TheLayout()
   {
      var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4), Children = { _newSession } };
      DockPanel.SetDock(bar, Dock.Top);
      DockPanel.SetDock(_failure, Dock.Bottom);
      return new DockPanel { Children = { bar, _failure, _tabs } };
   }
}
