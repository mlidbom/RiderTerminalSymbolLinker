using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.ProjectsDashboard;
using Cloyd.Sessions;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

/// <summary>A linked checkout sits inside a different repository's folder. The helper's window label names
/// the main checkout, while the helper actually works in that linked checkout.</summary>
public class When_Magnus_arranges_the_dashboard_tabs : SpecificationOpeningWindows
{
   readonly SandboxProject _alpha, _elsewhere;
   readonly DirectoryInfo _linked, _empty;
   readonly SessionHostId _first, _second;
   readonly ProjectsDashboardWindow _dashboard;
   readonly DashboardProject _alphaGroup, _elsewhereGroup;
   readonly DashboardWorktree _linkedGroup, _emptyGroup;

   public When_Magnus_arranges_the_dashboard_tabs()
   {
      _alpha = Sandbox.Project("Alpha");
      _elsewhere = Sandbox.Project("Elsewhere");
      var repository = GitRepositoryOfASpecification.In(new DirectoryInfo(_alpha.Project.Folder));
      GitRepositoryOfASpecification.In(new DirectoryInfo(_elsewhere.Project.Folder));
      _linked = new DirectoryInfo(Path.Combine(_elsewhere.Project.Folder, "A helpers checkout"));
      _empty = new DirectoryInfo(Path.Combine(_elsewhere.Project.Folder, "Unused checkout"));
      repository.HasAWorktreeIn(_linked, "fix-the-editor");
      repository.HasAWorktreeIn(_empty, "another-branch");
      _first = StartHelperShell();
      _second = StartHelperShell();
      var window = Open(_alpha.Window);
      Open(_elsewhere.Window);
      WaitUntil(() => window.BringingBackTheTabs.IsCompleted && window.Tabs.Count == 2, "the two helper tabs reopened in the main project's window");
      _dashboard = OnTheUIThread(() => Sandbox.WindowsOfCloyd.ShowDashboard(_alpha.Project));
      WaitUntil(() => _dashboard.Tree.Projects.Any(it => it.Project?.Equals(_alpha.Project) == true && it.Children.OfType<DashboardWorktree>().Count() == 3)
                   && _dashboard.Tree.Projects.Count() == 2, "Git grouped the main worktree, the external linked worktree and the empty worktree");
      _alphaGroup = OnTheUIThread(() => _dashboard.Tree.Projects.Single(it => it.Project!.Equals(_alpha.Project)));
      _elsewhereGroup = OnTheUIThread(() => _dashboard.Tree.Projects.Single(it => it.Project!.Equals(_elsewhere.Project)));
      _linkedGroup = OnTheUIThread(() => _alphaGroup.Children.OfType<DashboardWorktree>().Single(it => it.StartFolder.Folder == _linked.FullName));
      _emptyGroup = OnTheUIThread(() => _alphaGroup.Children.OfType<DashboardWorktree>().Single(it => it.StartFolder.Folder == _empty.FullName));
   }

   SessionHostId StartHelperShell() => Sandbox.HubOfTheSandbox.Hub.StartASessionHost(Sandbox.ShellOfItsSessions,
      _linked.FullName, _alpha.LabelOfCloydsHosts).GetAwaiter().GetResult().Id;

   [XF] public void the_worktree_and_its_sessions_belong_to_the_repository_that_registered_it()
   {
      OnTheUIThread(() => _linkedGroup.Sessions.Select(it => it.Host!.Id).ToHashSet().SetEquals([_first, _second])).Must().BeTrue();
      OnTheUIThread(() => _elsewhereGroup.Sessions.Count()).Must().Be(0);
      OnTheUIThread(() => _emptyGroup.Sessions.Count()).Must().Be(0);
      OnTheUIThread(() => _emptyGroup.CanStartSession).Must().BeTrue();
      Render(_dashboard, "Projects-Dashboard-vertical-tabs");
   }

   [XF] public void collapsing_either_level_leaves_the_active_tab_visible_and_its_terminal_untouched()
   {
      var active = OnTheUIThread(() => _linkedGroup.Sessions.First());
      var view = OnTheUIThread(() => { _dashboard.Select(active); return _dashboard.View!; });
      OnTheUIThread(() => view.SwitchBetweenTheTerminalAndTheChat());
      WaitUntil(() => !view.ShowsTheChat && view.Terminal.Size is not null && view.Terminal.IsKeyboardFocusWithin, "the dashboard terminal took focus");
      var connection = OnTheUIThread(() => view.Terminal.SessionId);
      ClickRow(_linkedGroup);
      OnTheUIThread(() => _linkedGroup.Expanded).Must().BeFalse();
      OnTheUIThread(() => _linkedGroup.Summary).Must().Be("2 sessions");
      ClickRow(_alphaGroup);
      OnTheUIThread(() => _alphaGroup.Expanded).Must().BeFalse();
      OnTheUIThread(() => _dashboard.Tree.VisibleRows.OfType<DashboardSession>().Single()).Must().ReferenceEqual(active);
      OnTheUIThread(() => _dashboard.View).Must().ReferenceEqual(view);
      OnTheUIThread(() => view.Terminal.SessionId).Must().Be(connection);
      OnTheUIThread(() => view.Terminal.IsKeyboardFocusWithin).Must().BeTrue();
      ClickRow(_alphaGroup);
      OnTheUIThread(() => _linkedGroup.Expanded).Must().BeFalse();
   }

   [XF] public void the_plus_on_an_empty_linked_worktree_starts_the_session_there()
   {
      var start = OnTheUIThread(() => _dashboard.GetVisualDescendants().OfType<NewSessionButton>().Single(it => it.DataContext == _emptyGroup));
      Focus(start);
      Press(_dashboard, Key.Space, PhysicalKey.Space, RawInputModifiers.None);
      WaitUntil(() => start.ShowsTheChoiceOfAgentCli, "the worktree's new session button offered both agents");
      OnTheUIThread(() => start.Choose(AgentCli.Codex));
      Type(_dashboard, "From the empty worktree");
      Press(_dashboard, Key.Enter, PhysicalKey.Enter, RawInputModifiers.None);
      WaitUntil(() => _emptyGroup.Sessions.SingleOrDefault()?.Name == "From the empty worktree", "the new Codex session appeared beneath the clicked worktree");
      OnTheUIThread(() => _dashboard.View!.Host.Directory).Must().Be(_empty.FullName);
      OnTheUIThread(() => _dashboard.View!.Host.Project).Must().Be(_alpha.Project);
   }

   [XF] public void the_repository_and_its_empty_worktrees_can_be_reopened_after_the_last_project_tab_is_gone()
   {
      Close(OnTheUIThread(() => Sandbox.WindowsOfCloyd.Open.Single(it => it.Project.Equals(_alpha.Project))));
      _alpha.End(_first);
      _alpha.End(_second);
      WaitUntil(() => !_alphaGroup.Sessions.Any(), "the repository had no sessions left");
      Close(_dashboard);
      var reopened = OnTheUIThread(() => Sandbox.WindowsOfCloyd.ShowDashboard(_elsewhere.Project));
      WaitUntil(() => reopened.Tree.Projects.Any(it => it.Project?.Equals(_alpha.Project) == true
                   && it.Children.OfType<DashboardWorktree>().Count() == 3 && !it.Sessions.Any()),
                "the remembered repository showed all three empty worktrees");
   }

   [XF] public void Escape_cancels_a_group_drag_without_changing_its_position_or_the_active_session()
   {
      var active = OnTheUIThread(() => _dashboard.Tree.ActiveSession);
      OnTheUIThread(() =>
      {
         _dashboard.MouseDown(PointOf(_elsewhereGroup, .5), MouseButton.Left);
         _dashboard.MouseMove(PointOf(_alphaGroup, .25), RawInputModifiers.LeftMouseButton);
      });
      Press(_dashboard, Key.Escape, PhysicalKey.Escape, RawInputModifiers.None);
      OnTheUIThread(() => _dashboard.MouseUp(PointOf(_alphaGroup, .25), MouseButton.Left));
      OnTheUIThread(() => _dashboard.Tree.Projects.First()).Must().ReferenceEqual(_alphaGroup);
      OnTheUIThread(() => _dashboard.Tree.ActiveSession).Must().ReferenceEqual(active);
   }

   [XF] public void dragging_projects_worktrees_and_tabs_moves_only_siblings_and_the_arrangement_survives_reopening()
   {
      var first = OnTheUIThread(() => _linkedGroup.Sessions.Single(it => it.Host!.Id == _first));
      var second = OnTheUIThread(() => _linkedGroup.Sessions.Single(it => it.Host!.Id == _second));
      var view = OnTheUIThread(() => _dashboard.View);
      Drag(_elsewhereGroup, _alphaGroup);
      Drag(_emptyGroup, _linkedGroup);
      Drag(second, first);
      Drag(second, _emptyGroup);
      ClickRow(_emptyGroup);
      OnTheUIThread(() => _dashboard.Tree.Projects.First()).Must().ReferenceEqual(_elsewhereGroup);
      OnTheUIThread(() => _alphaGroup.Children.IndexOf(_emptyGroup) < _alphaGroup.Children.IndexOf(_linkedGroup)).Must().BeTrue();
      OnTheUIThread(() => _linkedGroup.Sessions.First()).Must().ReferenceEqual(second);
      OnTheUIThread(() => _dashboard.View).Must().ReferenceEqual(view);
      Close(_dashboard);
      var reopened = OnTheUIThread(() => Sandbox.WindowsOfCloyd.ShowDashboard(_alpha.Project));
      WaitUntil(() =>
      {
         var project = reopened.Tree.Projects.SingleOrDefault(it => it.Project!.Equals(_alpha.Project));
         return project?.Children.OfType<DashboardWorktree>().Count() == 3;
      }, "the saved order was applied after Git rediscovered the worktrees");
      OnTheUIThread(() =>
      {
         reopened.Tree.Projects.First().Project.Must().Be(_elsewhere.Project);
         var group = reopened.Tree.Projects.Single(it => it.Project!.Equals(_alpha.Project));
         var worktrees = group.Children.OfType<DashboardWorktree>().ToList();
         var empty = worktrees.Single(it => it.StartFolder.Folder == _empty.FullName);
         var linked = worktrees.Single(it => it.StartFolder.Folder == _linked.FullName);
         (worktrees.IndexOf(empty) < worktrees.IndexOf(linked)).Must().BeTrue();
         empty.Expanded.Must().BeFalse();
         linked.Sessions.First().Host!.Id.Must().Be(_second);
      });
   }

   void ClickRow(DashboardRow row) => OnTheUIThread(() =>
   {
      var point = PointOf(row, .5);
      _dashboard.MouseDown(point, MouseButton.Left);
      _dashboard.MouseUp(point, MouseButton.Left);
      _dashboard.UpdateLayout();
   });

   void Drag(DashboardRow from, DashboardRow before) => OnTheUIThread(() =>
   {
      var start = PointOf(from, .5);
      var end = PointOf(before, .25);
      _dashboard.MouseDown(start, MouseButton.Left);
      _dashboard.MouseMove(end, RawInputModifiers.LeftMouseButton);
      _dashboard.MouseUp(end, MouseButton.Left);
      _dashboard.UpdateLayout();
   });

   Point PointOf(DashboardRow row, double down)
   {
      _dashboard.UpdateLayout();
      var control = _dashboard.GetVisualDescendants().OfType<ListBoxItem>().Single(it => it.DataContext == row);
      return control.TranslatePoint(new Point(95, control.Bounds.Height * down), _dashboard)!.Value;
   }

   public override void Dispose()
   {
      OnTheUIThread(() => Sandbox.WindowsOfCloyd.Dashboard?.Close());
      base.Dispose();
   }
}
