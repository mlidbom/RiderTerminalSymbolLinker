using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.ClaudeCode;
using Cloyd.History;
using Cloyd.Hub;
using Cloyd.Hub.Commands;
using Cloyd.Hub.RunningSessions;
using Cloyd.LocalApi.Contract;
using Cloyd.Projects;
using Cloyd.Saving;
using Cloyd.Sessions;
using Cloyd.VirtualDesktops;

namespace Cloyd.ProjectsDashboard;

/// <summary>The application's Projects Dashboard, with every project and session in a tree and an independent
/// view of the selected session beside it.</summary>
sealed partial class ProjectsDashboardWindow : Window, ISessionHistoryTarget
{
   public const string WindowTitle = "Projects Dashboard";
   readonly ProjectWindows _windows;
   readonly Project _openedFrom;
   readonly SessionHistoryPicker _history = new();
   readonly CancellationTokenSource _closed = new();
   readonly DispatcherTimer _save = new() { Interval = TimeSpan.FromSeconds(2) };
   SessionHostId? _restoreSelection;
   Session? _restoreOutsideHost;
   ConversationOutsideAHost? _outsideHost;
   bool _restoringSelection = true;
   VirtualDesktop? _desktop;
   SessionsOfCloyd Sessions => _windows.Sessions;
   public DashboardTree Tree { get; }
   public SessionView? View => SelectedSession.Content as SessionView;
   public SessionHistoryPicker HistoryPicker => _history;
   Project SelectedProject => (ProjectTree.SelectedItem as DashboardProject ?? (ProjectTree.SelectedItem as DashboardSession)?.Group)?.Project ?? _openedFrom;

   public ProjectsDashboardWindow(ProjectWindows windows, Project openedFrom)
   {
      _windows = windows;
      _openedFrom = openedFrom;
      Tree = new DashboardTree(windows);
      InitializeComponent();
      DataContext = Tree;
      Overlays.Children.Add(_history);
      _history.Chosen += entry => _ = ReportFailure(() => entry.Choose(this));
      _history.Closed += () => View?.FocusWhatItShows();
      AddHandler(KeyDownEvent, TakeShortcut, RoutingStrategies.Tunnel);
      _save.Tick += (_, _) => Save();
   }

   protected override void OnOpened(EventArgs e)
   {
      base.OnOpened(e);
      var saved = SavedProjectsDashboard.Read(Sessions.Folder.ProjectsDashboard);
      _desktop = saved.Desktop;
      if(_desktop is {} desktop) NativeWindow.Of(this)?.MoveTo(desktop);
      _restoreSelection = saved.Selected ?? _windows.Open.FirstOrDefault(it => it.Project.Equals(_openedFrom))?.SelectedTab?.Host.Id;
      _restoreOutsideHost = saved.OutsideHost;
      Sessions.Changed += Refresh;
      Sessions.Failed += Tell;
      Refresh();
      _save.Start();
   }

   protected override void OnClosed(EventArgs e)
   {
      _save.Stop();
      _closed.Cancel();
      Sessions.Changed -= Refresh;
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
         var outside = ProjectTree.SelectedItem as DashboardSession;
         new SavedProjectsDashboard(View?.Host.Id ?? _restoreSelection, _desktop, outside?.Host is null ? outside?.Latest : null).Write(Sessions.Folder.ProjectsDashboard);
      }
      catch(Exception failure) { Tell(failure); }
   }

   public void BringForward()
   {
      if(WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
      Activate();
   }

   void Refresh()
   {
      Tree.Refresh();
      if(_restoringSelection && Sessions.HasReadHosts && (_restoreOutsideHost is null || Sessions.HasReadRunningSessions))
      {
         var row = Tree.Sessions.FirstOrDefault(it => _restoreOutsideHost is not null && it.Latest?.Id == _restoreOutsideHost.Id)
                   ?? Tree.Sessions.FirstOrDefault(it => _restoreSelection is not null && it.Host?.Id == _restoreSelection)
                   ?? Tree.Sessions.FirstOrDefault(it => it.Group.Project?.Equals(_openedFrom) == true)
                   ?? Tree.Sessions.FirstOrDefault();
         _restoringSelection = false;
         _restoreSelection = null;
         if(row is not null) Select(row);
      }
      if(View is {} view) Sessions.ShowPeerNames(view);
      if(_outsideHost is {} outside && ProjectTree.SelectedItem is DashboardSession { Running: {} running }) outside.Update(running);
   }

   void SelectionChanged(object? sender, SelectionChangedEventArgs e)
   {
      if(!ReferenceEquals(e.Source, ProjectTree)) return;
      CloseView();
      if(ProjectTree.SelectedItem is not DashboardSession row) return;
      _restoringSelection = false;
      _restoreSelection = null;
      if(row.Host is not {} host)
      {
         if(row.Running is RunningCodex && !Sessions.OffersCodexChat)
            SelectedSession.Content = new TextBlock { Text = "This hub does not offer Codex chat.", Margin = new Avalonia.Thickness(16) };
         else if(row.Running is {} running)
         {
            _outsideHost = new ConversationOutsideAHost(Sessions, running);
            SelectedSession.Content = _outsideHost.Chat;
         }
         return;
      }
      var session = Sessions.Open(host, row.Latest);
      var view = new SessionView(session, inProjectWindow: false, showTerminal: !host.Attached || session.HasAttachment);
      SelectedSession.Content = view;
      session.Ended += SelectedHostEnded;
      Sessions.ShowPeerNames(view);
      Dispatcher.UIThread.Post(view.FocusWhatItShows, DispatcherPriority.Loaded);
   }

   void SelectedHostEnded(string why)
   {
      CloseView();
      ProjectTree.SelectedItem = null;
   }

   void CloseView()
   {
      var view = View;
      SelectedSession.Content = null;
      _outsideHost?.Chat.InputBox.Release();
      _outsideHost = null;
      if(view is null) return;
      view.Session.Ended -= SelectedHostEnded;
      Sessions.Release(view, keepTerminal: !_closed.IsCancellationRequested);
   }

   public void Select(DashboardSession row)
   {
      if(ProjectTree.ContainerFromItem(row.Group) is TreeViewItem group) group.IsExpanded = true;
      ProjectTree.SelectedItem = row;
      Dispatcher.UIThread.Post(() => ProjectTree.TreeContainerFromItem(row)?.BringIntoView(), DispatcherPriority.Loaded);
   }

   public WindowInTheAnswer? ShowSession(ClaudeCodeSessionId? session, ClaudeCodeProcessId? process)
   {
      var rows = Tree.Sessions.Where(it => it.Host is not null).ToArray();
      var row = rows.FirstOrDefault(it => process is not null && it.Running is RunningClaudeCode claude && claude.Process == process)
                ?? rows.FirstOrDefault(it => session is not null && it.Latest?.Id == session);
      if(row?.Group.Project is not {} project) return null;
      Select(row);
      BringForward();
      return new WindowInTheAnswer(project.Folder, WindowTitle);
   }

   void PrepareNewSessionButton(object? sender, RoutedEventArgs e)
   {
      var button = (NewSessionButton)sender!;
      if(button.Tag is not null) return;
      button.Tag = this;
      button.Opened += () => _ = ReportFailure(async () => button.Offer(await Sessions.Hub.AgentClisItServes(_closed.Token)));
      button.NewSessionAsked += session =>
      {
         if(button.DataContext is DashboardProject { Project: {} project }) _ = ReportFailure(() => Start(project, session));
      };
   }

   public Task Start(Project project, NewSession session) => StartInNewHost(project, null, session, session.Command);

   void ShowHost(SessionHostOfTheHub host, Session? latest, NewSession? startedAs)
   {
      _closed.Token.ThrowIfCancellationRequested();
      Sessions.Open(host, latest, startedAs);
      Sessions.Include(host);
      Refresh();
      Select(Tree.Sessions.Single(it => it.Host?.Id == host.Id));
   }

   public void ShowInATab(SessionHostOfTheHub host, Session session) => ShowHost(host, session, null);

   public Task ResumeInANewTab(Session session) => StartInNewHost(SelectedProject, session, null, session.CommandThatResumesIt);

   async Task StartInNewHost(Project project, Session? latest, NewSession? startedAs, AgentCliCommand command)
   {
      var host = await Sessions.StartHost(project, project.Folder, _closed.Token);
      ShowHost(host, latest, startedAs);
      var view = View!;
      _windows.Open.FirstOrDefault(it => it.Project.Equals(project))?.Include(view.Session);
      await Sessions.StartIn(view, command, _closed.Token);
   }

   public Task ChooseExistingTab(ProjectWindow owner, SessionTab tab, Session session, bool resume) =>
      ChooseDashboardHost(tab.Host, session, resume);

   public async Task ChooseDashboardHost(SessionHostOfTheHub host, Session session, bool resume)
   {
      ShowInATab(host, session);
      if(resume) await Sessions.StartIn(View!, session.CommandThatResumesIt, _closed.Token);
   }

   void EndSession(object? sender, RoutedEventArgs e)
   {
      if(sender is Button { DataContext: DashboardSession { Host: {} host } })
         _ = ReportFailure(() => Sessions.End(host.Id));
   }

   void OpenHistory(object? sender, RoutedEventArgs e) => _ = OpenHistory();

   public Task OpenHistory() => ReportFailure(async () =>
   {
      _history.Open();
      try { _history.Show(await SessionHistory.ReadFor(SelectedProject, _windows, Sessions.Hub, _closed.Token)); }
      catch { _history.Close(); throw; }
   });

   void TakeShortcut(object? sender, KeyEventArgs key)
   {
      if(key is { Key: Key.Y, KeyModifiers: KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift })
         _ = OpenHistory();
      else if(_history.IsOpen) return;
      else if(key is { Key: Key.H, KeyModifiers: KeyModifiers.Control }) View?.SwitchBetweenTheTerminalAndTheChat();
      else if(key.Key == Key.Tab && key.KeyModifiers is KeyModifiers.Control or (KeyModifiers.Control | KeyModifiers.Shift))
      {
         var rows = Tree.Sessions.ToList();
         if(rows.Count == 0) return;
         var index = rows.FindIndex(it => it == ProjectTree.SelectedItem);
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
