using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.ClaudeCode;
using Cloyd.DotNetAnalysisAndRefactoring;
using Cloyd.History;
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
/// It saves the virtual desktop it is on with its tabs (<see cref="Desktop"/>), and goes back to that desktop as it opens:
/// Magnus keeps each IDE window on a desktop of its own, and Cloyd's window where he puts it, mostly beside the IDE's.
/// A window with no desktop saved, as on its first opening, goes to the desktop of the project's window in an IDE, once
/// an IDE shows the project. It follows neither afterwards: where Magnus moves it, it stays.
/// </remarks>
sealed class ProjectWindow : Window
{
   static readonly TimeSpan BeforeFollowingTheRunningSessionsAgain = TimeSpan.FromSeconds(1);
   static readonly TimeSpan BetweenSaves = TimeSpan.FromSeconds(2);
   static readonly TimeSpan BetweenLooksForItsIdesWindow = TimeSpan.FromSeconds(2);
   static readonly TimeSpan BetweenReadingsOfItsFiles = TimeSpan.FromSeconds(5);

   /// <summary>How long the symbols the window gathered last serve: one gathering takes Compze's solution 6.4 s of Rider's time.</summary>
   static readonly TimeSpan BetweenGatheringsOfItsSymbols = TimeSpan.FromMinutes(2);

   /// <summary>How soon the window asks for the symbols again after Rider didn't answer, as while it still loads the solution, which it does as Cloyd's window opens.</summary>
   static readonly TimeSpan BeforeAskingAgainForItsSymbols = TimeSpan.FromSeconds(10);

   readonly ProjectWindows _windowsOfCloyd;
   readonly TheHub _hub;
   readonly TheIdes _ides;
   readonly ShellOfNewSessions _shellOfNewSessions;
   readonly SavedProjectWindows _savedWindows;
   readonly TabControl _tabs = new();
   readonly NewSessionButton _newSession = new();
   readonly Button _openTheHistory = new() { Content = "History" };
   readonly ClaudeSessionHistoryPicker _historyPicker;
   readonly TextBlock _failure = new() { IsVisible = false, Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 4) };
   readonly CancellationTokenSource _closed = new();

   /// <summary>Whether the window saves itself: not until it has brought back the saved tabs, so that it never saves a window they aren't in yet.</summary>
   bool _savesItself;

   /// <summary>
   /// The window of <paramref name="project"/>, one of <paramref name="windowsOfCloyd"/> from when it opens until it
   /// closes: its history picker tells which of their tabs runs what.
   /// </summary>
   public ProjectWindow(Project project, ProjectWindows windowsOfCloyd, TheHub hub, TheIdes ides, DotNetAnalysisAndRefactoringMcp dotNetAnalysisAndRefactoringMcp, ShellOfNewSessions shellOfNewSessions, SavedProjectWindows savedWindows)
   {
      Project = project;
      FilesAndSymbols = new FilesAndSymbolsOfAProject(project, dotNetAnalysisAndRefactoringMcp);
      _windowsOfCloyd = windowsOfCloyd;
      _hub = hub;
      _historyPicker = new ClaudeSessionHistoryPicker(this);
      _ides = ides;
      _shellOfNewSessions = shellOfNewSessions;
      _savedWindows = savedWindows;
      Title = $"Cloyd: {project.Name}";
      Width = 1400;
      Height = 900;
      Content = TheLayout();
      _newSession.NewSessionAsked += name => _ = TellingOfFailure(() => StartANewSession(name));
      ToolTip.SetTip(_openTheHistory, TipOfTheHistoryButton);
      _openTheHistory.Click += (_, _) => _ = OpenTheHistoryPicker();
      AddHandler(KeyDownEvent, OpenTheHistoryPickerOnItsKey, RoutingStrategies.Tunnel);
      _historyPicker.Chosen += entry => _ = TellingOfFailure(() => entry.Choose(this));
      _historyPicker.Closed += () => SelectedTab?.Terminal.Focus();
      _tabs.SelectionChanged += (_, _) => Dispatcher.UIThread.Post(() => SelectedTab?.Terminal.Focus(), DispatcherPriority.Loaded);
   }

   /// <summary>The tip of the button that opens the session history picker: the key that does too.</summary>
   public const string TipOfTheHistoryButton = "Resume a session: Ctrl+Alt+Shift+Y";

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
   /// The virtual desktop the window is on, which it saves: the one it went to as it opened, then the one Windows says it
   /// is on each time it saves, as after Magnus moved it. Null while it is on none it knows of: until it goes to one, and
   /// on Avalonia's headless platform, which Windows doesn't know, until it went to one.
   /// </summary>
   public VirtualDesktop? Desktop { get; private set; }

   /// <summary>
   /// Whether the window is on the virtual desktop shown, where Magnus is. A window of Avalonia's headless platform, which
   /// Windows doesn't know, is on no desktop but the one shown.
   /// </summary>
   public bool IsOnTheCurrentVirtualDesktop => NativeWindow.Of(this)?.IsOnTheCurrentVirtualDesktop ?? true;

   protected override void OnOpened(EventArgs e)
   {
      base.OnOpened(e);
      _windowsOfCloyd.Opened(this);
      var saved = _savedWindows.Of(Project);
      _ = GoToItsDesktop(saved.Desktop);
      BringingBackTheTabs = TellingOfFailure(async () =>
      {
         await BringBackTheTabs(saved);
         _ = KeepItSaved();
      });
      _ = FollowTheRunningSessions();
      _ = KeepItsFilesCurrent();
      _ = KeepItsSymbolsCurrent();
   }

   protected override void OnClosed(EventArgs e)
   {
      _closed.Cancel();
      if(_savesItself) TellingOfFailure(Save);

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

   /// <summary>Selects <paramref name="tab"/>, one of the window's <see cref="Tabs"/>, and brings the window forward.</summary>
   public void ShowTheTab(SessionTab tab)
   {
      Select(tab);
      BringForward();
   }

   /// <summary>The session history picker laid over the window, which <see cref="OpenTheHistoryPicker"/> opens.</summary>
   public ClaudeSessionHistoryPicker HistoryPicker => _historyPicker;

   /// <summary>
   /// Opens the session history picker over the window, as the History button and Ctrl+Alt+Shift+Y do, and shows the
   /// history in it once it has read it. Done once it shows it, or the window has told why not and closed it.
   /// </summary>
   public Task OpenTheHistoryPicker() => TellingOfFailure(async () =>
   {
      _historyPicker.Open();
      try
      {
         _historyPicker.Show(await ClaudeSessionHistory.ReadFor(Project, _windowsOfCloyd, _hub, _closed.Token));
      }
      catch
      {
         _historyPicker.Close(); // The window's foot tells why, not the picker, which would wait for the history.
         throw;
      }
   });

   /// <summary>Resumes <paramref name="session"/> in a new tab, in the project's folder, which it selects, as choosing it in the history picker does.</summary>
   public async Task ResumeInANewTab(ClaudeSession session)
   {
      var tab = await ShowANewHost(Project.Folder, session, nameGiven: null);
      _tabs.SelectedItem = tab;
      await StartClaudeCodeIn(tab, ClaudeCodeCommand.Resuming(session));
   }

   /// <summary>
   /// Resumes <paramref name="session"/> in <paramref name="tab"/>, one of the window's <see cref="Tabs"/>, whose Claude
   /// Code has exited to its shell: selects it, and brings the window forward.
   /// </summary>
   public async Task ResumeIn(SessionTab tab, ClaudeSession session)
   {
      ShowTheTab(tab);
      await StartClaudeCodeIn(tab, ClaudeCodeCommand.Resuming(session));
   }

   /// <summary>
   /// Shows <paramref name="host"/>, which runs <paramref name="session"/> and which no tab shows, in a tab of its own, which
   /// it selects: attaching takes the session over from a console that shows it.
   /// </summary>
   public void ShowInATab(SessionHostOfTheHub host, ClaudeSession session) => _tabs.SelectedItem = Show(host, session, nameGiven: null);

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
   /// Brings back the tabs the project's last window <paramref name="saved"/>, in their order, then shows the project's
   /// other hosts that no attach client shows, and selects the tab that was selected: else the first.
   /// </summary>
   async Task BringBackTheTabs(SavedProjectWindow saved)
   {
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

   /// <summary>Saves the window, on the <see cref="Desktop"/> it was on as it last looked: as it closes, Windows no longer knows it.</summary>
   void Save() => _savedWindows.Save(Project, new SavedProjectWindow([.. Tabs.Select(it => it.AsSaved)], SelectedTab?.Host.Id, Desktop));

   /// <summary>Saves the window as it is now, on the desktop Windows says it is on.</summary>
   void SaveAsItIsNow()
   {
      if(NativeWindow.Of(this)?.Desktop is {} desktop) Desktop = desktop;
      Save();
   }

   /// <summary>The size of the terminals shown, which a new tab's host is attached at: the default size of a host's console before any is shown.</summary>
   ConsoleSize SizeOfTheTerminals => SelectedTab?.Terminal.Size is { IsASize: true } size ? size : ConsoleSize.Default;

   /// <summary>Saves the window every <see cref="BetweenSaves"/>, from now until it closes, which saves it once more.</summary>
   async Task KeepItSaved()
   {
      _savesItself = true;
      while(!_closed.IsCancellationRequested)
      {
         TellingOfFailure(SaveAsItIsNow);
         await Task.Delay(BetweenSaves, CancellationToken.None);
      }
   }

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
   /// Moves the window to the virtual desktop it was <paramref name="saved"/> on, else, as on its first opening, to the
   /// desktop of the project's window in an IDE, once an IDE shows the project: it looks every
   /// <see cref="BetweenLooksForItsIdesWindow"/> until one does, or the window closes.
   /// </summary>
   async Task GoToItsDesktop(VirtualDesktop? saved)
   {
      if(saved is {} desktop)
      {
         TellingOfFailure(() => GoTo(desktop));
         return;
      }

      while(Desktop is null && !_closed.IsCancellationRequested)
      {
         await TellingOfFailure(async () =>
         {
            if((await _ides.WindowOf(Project, _closed.Token))?.Desktop is {} desktopOfItsIdesWindow) GoTo(desktopOfItsIdesWindow);
         });
         if(Desktop is null) await Task.Delay(BetweenLooksForItsIdesWindow, CancellationToken.None);
      }
   }

   void GoTo(VirtualDesktop desktop)
   {
      Desktop = desktop;
      NativeWindow.Of(this)?.MoveTo(desktop);
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

   /// <summary>Opens the session history picker on Ctrl+Alt+Shift+Y, before the terminal that has the focus takes the key for Claude Code.</summary>
   void OpenTheHistoryPickerOnItsKey(object? sender, KeyEventArgs key)
   {
      if(key is not { Key: Key.Y, KeyModifiers: KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift }) return;

      key.Handled = true;
      _ = OpenTheHistoryPicker();
   }

   Control TheLayout()
   {
      var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4), Spacing = 4, Children = { _newSession, _openTheHistory } };
      DockPanel.SetDock(bar, Dock.Top);
      DockPanel.SetDock(_failure, Dock.Bottom);
      return new Panel { Children = { new DockPanel { Children = { bar, _failure, _tabs } }, _historyPicker } };
   }
}
