using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.ClaudeCode;
using Cloyd.History;
using Cloyd.Hub;
using Cloyd.Hub.Commands;
using Cloyd.Hub.RunningSessions;
using Cloyd.LocalApi.Contract;
using Cloyd.Projects;
using Cloyd.Projects.Git;
using Cloyd.Saving;
using Cloyd.Sessions;
using Cloyd.VirtualDesktops;

namespace Cloyd.ProjectsDashboard;

/// <summary>The application's Projects Dashboard, with every project and session in a tree and an independent
/// view of the selected session beside it.</summary>
sealed partial class ProjectsDashboardWindow : Window
{
   public const string WindowTitle = "Projects Dashboard";
   readonly ProjectWindows _windows;
   readonly Project? _openedFrom;
   readonly SessionHistoryPicker _history = new();
   readonly CancellationTokenSource _closed = new();
   readonly DispatcherTimer _save = new() { Interval = TimeSpan.FromSeconds(2) };
   SessionHostId? _restoreSelection;
   Session? _restoreOutsideHost;
   ConversationOutsideAHost? _outsideHost;
   bool _restoringSelection = true;
   bool _resuming;
   IPointer? _dismissSearchOnRelease;
   VirtualDesktop? _desktop;
   SessionsOfCloyd Sessions => _windows.Sessions;
   public DashboardTree Tree { get; }
   public SessionView? View => SelectedSession.Content as SessionView;
   public SessionHistoryPicker HistoryPicker => _history;
   public ProjectsDashboardWindow(ProjectWindows windows, Project? openedFrom)
   {
      _windows = windows;
      _openedFrom = openedFrom;
      Tree = new DashboardTree(windows);
      InitializeComponent();
      DataContext = Tree;
      Tree.ActiveSessionChanged += ShowActiveSession;
      Tabs.TabChosen += Select;
      Tabs.ContainerPrepared += (_, prepared) => prepared.Container.ContextMenu = MenuFor(Tabs.Items[prepared.Index]);
      Overlays.Children.Add(_history);
      _history.Chosen += entry =>
      {
         if(Tree.Find(entry.Id) is {} row) Select(row);
         else Tell(new InvalidOperationException($"The session {entry.Title} is no longer available."));
      };
      _history.Closed += () => View?.FocusWhatItShows();
      AddHandler(KeyDownEvent, TakeShortcut, RoutingStrategies.Tunnel);
      TreeSearchBox.LostFocus += (_, _) => DismissEmptySearchAfterFocusMoves();
      Deactivated += (_, _) => { _dismissSearchOnRelease = null; DismissEmptySearchAfterFocusMoves(); };
      AddHandler(PointerPressedEvent, OnSearchClickOutside, RoutingStrategies.Tunnel, handledEventsToo: true);
      AddHandler(PointerReleasedEvent, FinishSearchClickOutside, RoutingStrategies.Bubble, handledEventsToo: true);
      _save.Tick += (_, _) => Save();
   }

   protected override void OnOpened(EventArgs e)
   {
      base.OnOpened(e);
      var saved = SavedProjectsDashboard.Read(Sessions.Folder.ProjectsDashboard);
      Tree.Arrangement = saved.Arrangement ?? new();
      _desktop = saved.Desktop;
      if(_desktop is {} desktop) NativeWindow.Of(this)?.MoveTo(desktop);
      _restoreSelection = saved.Selected ?? _windows.Open.FirstOrDefault(it => it.Project.Equals(_openedFrom))?.SelectedTab?.Host.Id;
      _restoreOutsideHost = saved.OutsideHost;
      Sessions.Changed += Refresh;
      Sessions.Catalogue.Changed += Refresh;
      Sessions.Failed += Tell;
      Refresh();
      _save.Start();
   }

   protected override void OnClosed(EventArgs e)
   {
      _save.Stop();
      _closed.Cancel();
      Sessions.Changed -= Refresh;
      Sessions.Catalogue.Changed -= Refresh;
      Tree.ActiveSessionChanged -= ShowActiveSession;
      Sessions.Failed -= Tell;
      Save();
      CloseView();
      foreach(var row in Tree.Sessions)
         if(row.Host is {} host && Sessions.Known(host.Id) is { HasViews: false } session) session.LetGo();
      base.OnClosed(e);
   }

   void Save()
   {
      try
      {
         _desktop = NativeWindow.Of(this)?.Desktop ?? _desktop;
         var outside = Tree.ActiveSession;
         new SavedProjectsDashboard(View?.Host.Id ?? _restoreSelection, _desktop, outside?.Host is null ? outside?.Latest : null, Tree.Arrangement).Write(Sessions.Folder.ProjectsDashboard);
      }
      catch(Exception failure) { Tell(failure); }
   }

   public void BringForward()
   {
      if(WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
      Activate();
   }

   ContextMenu ProjectMenu(Project project)
   {
      var open = new MenuItem { Header = "Open project" };
      open.Click += (_, _) =>
      {
         try { _windows.OpenFromDashboard(project); }
         catch(Exception failure) { Tell(failure); }
      };
      return new ContextMenu { Items = { open } };
   }

   ContextMenu? MenuFor(object? row) => row switch
   {
      DashboardWorktree worktree => new WorktreeContextMenu(worktree, Tell, token => Sessions.Hub.PreviewWorktreeSessions(worktree.StartFolder, token),
         preview => _ = ReportFailure(() => RemoveWorktree(worktree, preview))),
      DashboardProject { Project: {} project } => ProjectMenu(project),
      _ => null
   };

   void Refresh()
   {
      _ = ReportFailure(() => Sessions.Catalogue.Read(_closed.Token));
      RefreshRows();
      _ = ReportFailure(() => Tree.ReadClosedProjectTabs(RefreshRows, _closed.Token));
      _ = ReportFailure(() => Tree.DiscoverWorktrees(RefreshRows, Tell, _closed.Token));
   }

   void RefreshRows()
   {
      Tree.Refresh();
      _ = ReportFailure(() => Tree.ReadGitStatus(_closed.Token));
      if(_restoringSelection && Sessions.HasReadHosts && (_restoreOutsideHost is null || Sessions.HasReadRunningSessions && Sessions.Catalogue.HasRead))
      {
         var row = Tree.Sessions.FirstOrDefault(it => _restoreOutsideHost is not null && it.Latest?.Id == _restoreOutsideHost.Id)
                   ?? Tree.Sessions.FirstOrDefault(it => _restoreSelection is not null && it.Host?.Id == _restoreSelection)
                   ?? Tree.Sessions.FirstOrDefault(it => it.Host?.Project?.Equals(_openedFrom) == true)
                   ?? Tree.Sessions.FirstOrDefault();
         _restoringSelection = false;
         _restoreSelection = null;
         if(row is not null) Select(row);
      }
      if(View is {} view) Sessions.ShowPeerNames(view);
      if(_outsideHost is {} outside && Tree.ActiveSession is { Latest: {} session } selected) outside.Update(session, selected.Running);
      ResumeBar.IsVisible = Tree.ActiveSession?.ReadyToResume == true;
   }

   void ShowActiveSession()
   {
      CloseView();
      ResumeBar.IsVisible = Tree.ActiveSession?.ReadyToResume == true;
      if(Tree.ActiveSession is not {} row) return;
      RevealTabIfListed(row);
      _restoringSelection = false;
      _restoreSelection = null;
      if(row.Host is not {} host)
      {
         if(row.Latest?.AgentCli == AgentCli.Codex && !Sessions.OffersCodexChat)
            SelectedSession.Content = new TextBlock { Text = "This hub does not offer Codex chat.", Margin = new Thickness(16) };
         else if(row.Latest is {} latest)
         {
            _outsideHost = new ConversationOutsideAHost(Sessions, latest, row.Running);
            SelectedSession.Content = _outsideHost.Chat;
         }
         return;
      }
      var session = Sessions.Open(host, row.Latest);
      var view = new SessionView(session, inProjectWindow: false, showTerminal: !host.Attached || session.HasAttachment);
      SelectedSession.Content = view;
      Sessions.ShowPeerNames(view);
      Dispatcher.UIThread.Post(view.FocusWhatItShows, DispatcherPriority.Loaded);
   }

   void CloseView()
   {
      var view = View;
      SelectedSession.Content = null;
      _outsideHost?.Chat.InputBox.Release();
      _outsideHost = null;
      if(view is null) return;
      Sessions.Release(view, keepTerminal: !_closed.IsCancellationRequested);
   }

   public void Select(DashboardSession row)
   {
      if(Tree.ActiveSession != row) Tree.Select(row);
      else
      {
         RevealTabIfListed(row);
         View?.FocusWhatItShows();
      }
   }

   void RevealTabIfListed(DashboardSession row)
   {
      Tabs.SelectedItem = Tree.VisibleRows.Contains(row) ? row : null;
      if(Tabs.SelectedItem is not null) Tabs.ScrollIntoView(row);
   }

   void TogglePin(object? sender, RoutedEventArgs e)
   {
      if(sender is not Control { DataContext: DashboardSession row }) return;
      try { Tree.TogglePin(row); }
      catch(Exception failure) { Tell(failure); }
   }

   public WindowInTheAnswer? ShowSession(ClaudeCodeSessionId? session, ClaudeCodeProcessId? process)
   {
      var rows = Tree.Sessions.Where(it => it.Host is not null).ToArray();
      var row = rows.FirstOrDefault(it => process is not null && it.Running is RunningClaudeCode claude && claude.Process == process)
                ?? rows.FirstOrDefault(it => session is not null && it.Latest?.Id == session);
      if(row?.Host is not {} host) return null;
      Select(row);
      BringForward();
      return new WindowInTheAnswer(host.Project?.Folder ?? host.Directory, WindowTitle);
   }

   void PrepareNewSessionButton(object? sender, RoutedEventArgs e)
   {
      var button = (NewSessionButton)sender!;
      if(button.Tag is not null) return;
      button.Tag = this;
      DashboardGroup? chosenGroup = null;
      button.Opened += () =>
      {
         chosenGroup = button.DataContext as DashboardGroup;
         button.AskForWorktreeName(chosenGroup is DashboardProject { CreatesWorktree: true });
         _ = ReportFailure(async () => button.Offer(await Sessions.Hub.AgentClisItServes(_closed.Token)));
      };
      button.NewSessionAsked += session =>
      {
         if(chosenGroup is DashboardProject { Project: {} project })
            _ = ReportFailure(() => StartFromProject(project, session));
         else if(chosenGroup is { StartFolder: {} folder } group)
            _ = ReportFailure(() => StartInNewHost(OwningProject(group, folder), folder, null, session, session.Command));
      };
   }

   Project OwningProject(DashboardGroup group, Project folder) =>
      _windows.Open.FirstOrDefault(it => it.Project.Equals(folder))?.Project ?? group.Group.Project ?? folder;

   public Task Start(Project project, NewSession session) => StartInNewHost(project, project, null, session, session.Command);

   async Task StartFromProject(Project project, NewSession session)
   {
      if(await GitWorktrees.Read(project, _closed.Token) is not {} repository)
      {
         await Start(project, session);
         return;
      }
      var worktree = await NewSessionWorktree.Create(repository.Repository, session.Name, _closed.Token);
      try
      {
         await Tree.RefreshWorktrees(RefreshRows, Tell, _closed.Token);
         await StartInNewHost(repository.Repository, worktree, null, session, session.Command);
      }
      catch(Exception failure) when(!_closed.IsCancellationRequested)
      {
         throw new InvalidOperationException($"Created {worktree}. Starting the session failed: {failure.Message}. Its + can start a session there.", failure);
      }
   }

   void ShowHost(SessionHostOfTheHub host, Session? latest, NewSession? startedAs)
   {
      _closed.Token.ThrowIfCancellationRequested();
      Sessions.Open(host, latest, startedAs);
      Sessions.Include(host);
      Refresh();
      Select(Tree.Sessions.Single(it => it.Host?.Id == host.Id));
   }

   async Task StartInNewHost(Project project, Project worktree, Session? latest, NewSession? startedAs, AgentCliCommand command)
   {
      var host = await Sessions.StartHost(project, worktree.Folder, _closed.Token);
      ShowHost(host, latest, startedAs);
      var view = View!;
      _windows.Open.FirstOrDefault(it => it.Project.Equals(project))?.Include(view.Session);
      await Sessions.StartIn(view, command, _closed.Token);
   }

   public async Task ChooseDashboardHost(SessionHostOfTheHub host, Session session, bool resume)
   {
      ShowHost(host, session, null);
      if(resume) await Sessions.StartIn(View!, session.CommandThatResumesIt, _closed.Token);
   }

   void EndSession(object? sender, RoutedEventArgs e)
   {
      if(sender is SessionTabHeader { DataContext: DashboardSession { Host: {} host } })
         _ = ReportFailure(() => Sessions.End(host.Id));
   }

   void OpenHistory(object? sender, RoutedEventArgs e) => _ = OpenHistory((sender as Control)?.DataContext as DashboardGroup);

   void ResetFilterOverrides(object? sender, RoutedEventArgs e) => Tree.ResetFilterOverrides();

   void ShowTreeSearch(object? sender, RoutedEventArgs e) => ShowTreeSearch();
   void ShowTreeSearch()
   {
      TreeSearchPanel.IsVisible = true;
      TreeSearchBox.Focus();
      TreeSearchBox.SelectAll();
   }

   void CloseTreeSearch(object? sender, RoutedEventArgs e) => CloseTreeSearch();
   void CloseTreeSearch()
   {
      Tree.SearchText = "";
      TreeSearchPanel.IsVisible = false;
      SearchTreeButton.Focus();
   }

   bool TreeSearchIsEmpty => TreeSearchPanel.IsVisible && string.IsNullOrWhiteSpace(TreeSearchBox.Text);

   /// <summary>The close button is inside the search panel. Hiding it during a Tab focus transfer
   /// can leave its keyboard-focus border attached when the search opens again.</summary>
   void DismissEmptySearchAfterFocusMoves()
   {
      if(!TreeSearchIsEmpty || _dismissSearchOnRelease is not null) return;
      Dispatcher.UIThread.Post(() =>
      {
         if(_dismissSearchOnRelease is null && (!IsActive || !TreeSearchBox.IsKeyboardFocusWithin)) HideEmptyTreeSearch();
      }, DispatcherPriority.Input);
   }

   void HideEmptyTreeSearch()
   {
      if(!TreeSearchIsEmpty) return;
      TreeSearchPanel.IsVisible = false;
      Tree.SearchText = "";
   }

   /// <summary>Hiding the search moves the rows up. Let the control under the pointer finish
   /// its click in the original position, even if pressing it takes focus or leaves focus in the search box.</summary>
   void OnSearchClickOutside(object? sender, PointerPressedEventArgs e)
   {
      if(TreeSearchIsEmpty && e.Source is Visual hit &&
         hit != TreeSearchBox && !TreeSearchBox.IsVisualAncestorOf(hit) &&
         hit != SearchTreeButton && !SearchTreeButton.IsVisualAncestorOf(hit))
         _dismissSearchOnRelease = e.Pointer;
   }

   void FinishSearchClickOutside(object? sender, PointerReleasedEventArgs e)
   {
      if(_dismissSearchOnRelease != e.Pointer) return;
      _dismissSearchOnRelease = null;
      HideEmptyTreeSearch();
   }

   public Task OpenHistory(DashboardGroup? group = null)
   {
      var title = group switch
      {
         DashboardWorktree checkout => $"History · {checkout.Group.Name} · {checkout.Name}",
         DashboardProject project => $"History · {project.Name}",
         _ => "History · All projects"
      };
      return ReportFailure(() => _history.Open(ReadScope, _closed.Token, title, fixedScope: true));

      async Task<SessionHistory> ReadScope(CancellationToken cancellation)
      {
         var history = await SessionHistory.ReadForAllProjects(_windows, Sessions.Hub, cancellation);
         cancellation.ThrowIfCancellationRequested();
         RefreshRows();
         if(group is null) return history;
         await Tree.DiscoverHistoryWorktrees(RefreshRows, Tell, _closed.Token, cancellation);
         return history.Within(Tree.Sessions.Where(it => it.BelongsTo(group)).Select(it => it.Latest?.Id).OfType<SessionId>().ToHashSet());
      }
   }

   void ResumeSelected(object? sender, RoutedEventArgs e) => _ = ReportFailure(ResumeSelected);

   public async Task ResumeSelected()
   {
      if(_resuming || Tree.ActiveSession is not { ReadyToResume: true, Latest: {} session } row) return;
      _resuming = true;
      ResumeButton.IsEnabled = false;
      try
      {
         var running = await Sessions.Hub.RunningSessions(_closed.Token);
         if(Tree.ActiveSession != row || row.Latest?.Id != session.Id) return;
         if(running.Any(it => it.Session.Id == session.Id))
         {
            if(Tree.Find(session.Id) is {} already) Select(already);
            return;
         }
         if(row.Host is {} host && View is {} view && !running.Any(it => it.SessionHost == host.Id))
            await Sessions.StartIn(view, session.CommandThatResumesIt, _closed.Token);
         else
         {
            var folder = new[] { row.WorkingFolder, row.Parent?.StartFolder, row.Group.Project, _openedFrom }.OfType<Project>()
                         .FirstOrDefault(it => Directory.Exists(it.Folder)) ?? throw new DirectoryNotFoundException($"No working folder is available for {row.Name}.");
            var owner = row.Group.Project is {} project && Directory.Exists(project.Folder) ? project : folder;
            await StartInNewHost(owner, folder, session, null, session.CommandThatResumesIt);
         }
      }
      finally { _resuming = false; ResumeButton.IsEnabled = true; }
   }

   void TakeShortcut(object? sender, KeyEventArgs key)
   {
      if(key.Key == Key.Escape && Tabs.CancelDrag()) key.Handled = true;
      else if(key is { Key: Key.Y, KeyModifiers: KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift })
         _ = OpenHistory();
      else if(_history.IsOpen) return;
      else if(key is { Key: Key.F, KeyModifiers: KeyModifiers.Control | KeyModifiers.Shift }) ShowTreeSearch();
      else if(key.Key == Key.Escape && TreeSearchPanel.IsKeyboardFocusWithin) CloseTreeSearch();
      else if(key is { Key: Key.H, KeyModifiers: KeyModifiers.Control }) View?.SwitchBetweenTheTerminalAndTheChat();
      else if(key.Key == Key.Tab && key.KeyModifiers is KeyModifiers.Control or (KeyModifiers.Control | KeyModifiers.Shift))
      {
         var rows = Tree.NavigableSessions.ToList();
         if(rows.Count == 0) return;
         var index = rows.FindIndex(it => it == Tree.ActiveSession);
         var step = key.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1;
         Select(rows[index < 0 ? step > 0 ? 0 : rows.Count - 1 : (index + step + rows.Count) % rows.Count]);
      }
      else return;
      key.Handled = true;
   }

   async Task ReportFailure(Func<Task> action)
   {
      try { await action(); }
      catch(OperationCanceledException) when(_closed.IsCancellationRequested) { }
      catch(Exception failure) { Tell(failure); }
   }

   void Tell(Exception failure)
   {
      FailureText.Text = failure.Message;
      FailureText.IsVisible = true;
   }
}
