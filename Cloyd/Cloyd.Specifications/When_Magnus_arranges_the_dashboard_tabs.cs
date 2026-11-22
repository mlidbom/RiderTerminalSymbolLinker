using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.ProjectsDashboard;
using Cloyd.ProjectsDashboard.Filters;
using Cloyd.ClaudeCode;
using Cloyd.Codex;
using Avalonia.Automation;
using Avalonia.Controls.Primitives;
using System.Diagnostics;
using Cloyd.Sessions;
using System.Text.Json;
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

   [XF] public void each_All_switch_temporarily_bypasses_only_its_strip_and_restores_its_choices_after_reopening()
   {
      Close(_dashboard);
      Sandbox.WindowsOfCloyd.Sessions.Folder.ProjectsDashboard.Write("""{"arrangement":{"rootInclusions":2,"activityFilter":6}}""");
      var window = OnTheUIThread(() => Sandbox.WindowsOfCloyd.ShowDashboard(_alpha.Project));
      WaitUntil(() => window.Tree.Sessions.Count() == 2, "the two tabs were in the dashboard");
      OnTheUIThread(() =>
      {
         var memberships = window.GetVisualDescendants().OfType<SessionFilterStrip>().Single(it => it.DataContext == window.Tree);
         var statuses = window.GetVisualDescendants().OfType<SessionStatusFilterStrip>().Single();
         Names(memberships).Must().SequenceEqual(["Pinned", "Open tabs", "Sessions outside tabs", "All session memberships"]);
         Names(statuses).Must().SequenceEqual(["Running", "Ready to resume", "Working", "Waiting for you", "Idle", "Error", "All statuses"]);
         Switch(window, "Error").IsChecked.Must().Be(true);
      });
      PressSwitch(window, "Waiting for you");
      PressSwitch(window, "All statuses");
      OnTheUIThread(() => window.Tree.NoMatches).Must().BeTrue();
      PressSwitch(window, "All session memberships");
      WaitUntil(() => window.Tree.NavigableSessions.Count() == 2, "both All switches admitted the two unpinned shells");
      OnTheUIThread(() =>
      {
         var selected = Switch(window, "Pinned");
         var unselected = Switch(window, "Open tabs");
         selected.IsEnabled.Must().BeTrue();
         HasVisibleBorder(selected).Must().BeTrue();
         HasVisibleBorder(unselected).Must().BeFalse();
      });
      Render(window, "Projects-Dashboard-switches-while-All-is-on");
      Close(window);
      var reopened = OnTheUIThread(() => Sandbox.WindowsOfCloyd.ShowDashboard(_alpha.Project));
      WaitUntil(() => reopened.Tree.NavigableSessions.Count() == 2, "both temporary All choices survived reopening");
      PressSwitch(reopened, "All statuses");
      OnTheUIThread(() =>
      {
         Switch(reopened, "Waiting for you").IsChecked.Must().Be(true);
         Switch(reopened, "Error").IsChecked.Must().Be(true);
         Switch(reopened, "Idle").IsChecked.Must().Be(false);
         reopened.Tree.NoMatches.Must().BeTrue();
      });
      PressSwitch(reopened, "All session memberships");
      OnTheUIThread(() =>
      {
         Switch(reopened, "Pinned").IsChecked.Must().Be(true);
         Switch(reopened, "Open tabs").IsChecked.Must().Be(false);
         Switch(reopened, "Sessions outside tabs").IsChecked.Must().Be(false);
      });
      PressSwitch(reopened, "All statuses");
      PressSwitch(reopened, "Error");
      OnTheUIThread(() =>
      {
         Switch(reopened, "All statuses").IsChecked.Must().Be(false);
         Switch(reopened, "Error").IsChecked.Must().Be(false);
         Switch(reopened, "Waiting for you").IsChecked.Must().Be(true);
      });
      PressSwitch(reopened, "Waiting for you");
      PressSwitch(reopened, "All statuses");
      OnTheUIThread(() => Switch(reopened, "All statuses").IsChecked).Must().Be(true);
      PressSwitch(reopened, "Idle");
      OnTheUIThread(() => Switch(reopened, "All statuses").IsChecked).Must().Be(false);
      PressSwitch(reopened, "Pinned");
      OnTheUIThread(() => Switch(reopened, "All session memberships").IsChecked).Must().Be(true);
      PressSwitch(reopened, "Open tabs");
      OnTheUIThread(() => Switch(reopened, "All session memberships").IsChecked).Must().Be(false);

      bool HasVisibleBorder(ToggleButton button) => button.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>()
         .Single(it => it.Name == "PART_ContentPresenter") is { BorderThickness.Left: > 0, BorderBrush: Avalonia.Media.ISolidColorBrush { Color.A: > 0 } };
      string[] Names(DashboardFilterStrip strip) => strip.Children.OfType<ToggleButton>().Select(it => AutomationProperties.GetName(it)!).ToArray();
      ToggleButton Switch(ProjectsDashboardWindow dashboard, string name) => dashboard.GetVisualDescendants().OfType<DashboardFilterStrip>().Where(it => it.DataContext == dashboard.Tree)
         .SelectMany(it => it.Children.OfType<ToggleButton>()).Single(it => AutomationProperties.GetName(it) == name);
      void PressSwitch(ProjectsDashboardWindow dashboard, string name)
      {
         Focus(OnTheUIThread(() => Switch(dashboard, name)));
         Press(dashboard, Key.Space, PhysicalKey.Space, RawInputModifiers.None);
      }
   }

   [XF] public void a_worktree_switch_cycles_On_Off_Unset_and_only_overrides_All_in_its_own_worktree()
   {
      OnTheUIThread(() =>
      {
         var tree = _dashboard.Tree;
         var filters = tree.Filters;
         tree.ToggleInclusion(null, IncludedSessions.AllMemberships);
         tree.EditingFilters = true;
         Cycle(IncludedSessions.Pinned);
         filters.LocalSetting(_linkedGroup, IncludedSessions.Pinned).Must().Be(true);
         filters.Effective(_linkedGroup).HasFlag(IncludedSessions.AllMemberships).Must().BeFalse();
         filters.Effective(_emptyGroup).HasFlag(IncludedSessions.AllMemberships).Must().BeTrue();
         Cycle(IncludedSessions.Pinned);
         filters.LocalSetting(_linkedGroup, IncludedSessions.Pinned).Must().Be(false);
         tree.ToggleInclusion(_alphaGroup, IncludedSessions.Pinned);
         filters.Effective(_linkedGroup).HasFlag(IncludedSessions.Pinned).Must().BeFalse();
         Cycle(IncludedSessions.Pinned);
         filters.IsInherited(_linkedGroup, IncludedSessions.Pinned).Must().BeTrue();
         filters.Effective(_linkedGroup).HasFlag(IncludedSessions.Pinned).Must().BeTrue();
         tree.ToggleInclusion(_alphaGroup, IncludedSessions.Pinned);
         filters.Effective(_linkedGroup).HasFlag(IncludedSessions.Pinned).Must().BeFalse();
         Cycle(IncludedSessions.AllMemberships);
         filters.IsInherited(_linkedGroup, IncludedSessions.AllMemberships).Must().BeTrue();
         Cycle(IncludedSessions.AllMemberships);
         filters.LocalSetting(_linkedGroup, IncludedSessions.AllMemberships).Must().Be(true);
         Cycle(IncludedSessions.AllMemberships);
         filters.LocalSetting(_linkedGroup, IncludedSessions.AllMemberships).Must().Be(false);

         void Cycle(IncludedSessions kind)
         {
            _dashboard.UpdateLayout();
            var name = kind == IncludedSessions.Pinned ? "Pinned" : "All session memberships";
            var strip = _dashboard.GetVisualDescendants().OfType<SessionFilterStrip>().Single(it => it.DataContext == _linkedGroup);
            var button = strip.Children.OfType<ToggleButton>().Single(it => AutomationProperties.GetName(it) == name);
            var centre = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), _dashboard)!.Value;
            _dashboard.MouseDown(centre, MouseButton.Left);
            _dashboard.MouseUp(centre, MouseButton.Left);
         }
      });
   }

   [XF] public void tree_search_reveals_matches_beneath_collapsed_headings_and_restores_the_view_when_dismissed()
   {
      OnTheUIThread(() => _dashboard.Tree.Toggle(_alphaGroup));
      var selected = OnTheUIThread(() => _dashboard.Tree.ActiveSession);
      var search = OnTheUIThread(() => _dashboard.FindControl<TextBox>("TreeSearchBox")!);
      var chat = OnTheUIThread(() =>
      {
         var view = _dashboard.View!;
         if(!view.ShowsTheChat) view.SwitchBetweenTheTerminalAndTheChat();
         view.FocusWhatItShows();
         return view.Chat!;
      });
      WaitUntil(() => chat.InputBox.IsKeyboardFocusWithin, "the transcript had focus");
      Press(_dashboard, Key.F, PhysicalKey.F, RawInputModifiers.Control);
      WaitUntil(() => chat.FindBar.HasTheKeyboard, "Ctrl+F opened transcript search");
      OnTheUIThread(() => search.IsEffectivelyVisible).Must().BeFalse();
      Press(_dashboard, Key.F, PhysicalKey.F, RawInputModifiers.Control | RawInputModifiers.Shift);
      WaitUntil(() => search.IsKeyboardFocusWithin, "Ctrl+Shift+F focused the newly visible search box");
      Type(_dashboard, "ALPHA fix-the-editor shell");
      WaitUntil(() => _dashboard.Tree.VisibleRows.Count == 4 && _dashboard.Tree.VisibleRows.Contains(_linkedGroup)
                      && _dashboard.Tree.NavigableSessions.Count() == 2, "the project, branch and session name matched together");
      OnTheUIThread(() => _dashboard.Tree.ActiveSession).Must().ReferenceEqual(selected!);
      Type(_dashboard, " other");
      WaitUntil(() => _dashboard.Tree.NoMatches, "typing removed the rows immediately");
      Press(_dashboard, Key.Escape, PhysicalKey.Escape, RawInputModifiers.None);
      WaitUntil(() => !search.IsEffectivelyVisible && !_alphaGroup.ShownExpanded && _dashboard.Tree.SearchText == "",
                "Escape hid the box and restored the collapsed project");
      OnTheUIThread(() => chat.FindBar.IsOpen).Must().BeTrue();
      Click(OnTheUIThread(() => _dashboard.FindControl<Button>("SearchTreeButton")!));
      WaitUntil(() => search.IsKeyboardFocusWithin, "the search icon focused the box");
      Type(_dashboard, "unused checkout");
      WaitUntil(() => _dashboard.Tree.VisibleRows.SequenceEqual([_alphaGroup, _emptyGroup]), "the search found an empty worktree with its project heading");
      var allGroups = OnTheUIThread(() => _dashboard.GetVisualDescendants().OfType<ToggleButton>().Single(it => AutomationProperties.GetName(it) == "All groups"));
      Focus(allGroups);
      OnTheUIThread(() => search.IsEffectivelyVisible && _dashboard.Tree.SearchText == "unused checkout").Must().BeTrue();
      Press(_dashboard, Key.F, PhysicalKey.F, RawInputModifiers.Control | RawInputModifiers.Shift);
      Press(_dashboard, Key.Back, PhysicalKey.Backspace, RawInputModifiers.None);
      WaitUntil(() => _dashboard.Tree.SearchText == "" && search.IsKeyboardFocusWithin, "clearing the focused query left the box ready for typing");
      Focus(allGroups);
      WaitUntil(() => !search.IsEffectivelyVisible && allGroups.IsKeyboardFocusWithin, "leaving the empty box hid it and kept the target's focus");
      Click(OnTheUIThread(() => _dashboard.FindControl<Button>("SearchTreeButton")!));
      WaitUntil(() => search.IsKeyboardFocusWithin, "the empty box reopened");
      ClickRow(_alphaGroup);
      WaitUntil(() => !search.IsEffectivelyVisible && _alphaGroup.Expanded, "clicking the collapsed heading hid the empty box and expanded the heading under the pointer");
   }

   [XF] public void tabbing_out_of_a_nonempty_tree_search_keeps_the_query_and_moves_keyboard_focus()
   {
      var search = OnTheUIThread(() => _dashboard.FindControl<TextBox>("TreeSearchBox")!);
      var close = OnTheUIThread(() => _dashboard.GetVisualDescendants().OfType<Button>().Single(it => AutomationProperties.GetName(it) == "Close tree search"));
      Press(_dashboard, Key.F, PhysicalKey.F, RawInputModifiers.Control | RawInputModifiers.Shift);
      WaitUntil(() => search.IsKeyboardFocusWithin, "the tree search received the keyboard");
      Press(_dashboard, Key.Tab, PhysicalKey.Tab, RawInputModifiers.None);
      WaitUntil(() => !search.IsEffectivelyVisible, "the empty search closed on Tab");
      Press(_dashboard, Key.F, PhysicalKey.F, RawInputModifiers.Control | RawInputModifiers.Shift);
      WaitUntil(() => search.IsKeyboardFocusWithin, "the tree search took focus again");
      Type(_dashboard, "shell");
      for(var visit = 0; visit < 2; visit++)
      {
         Press(_dashboard, Key.Tab, PhysicalKey.Tab, RawInputModifiers.None);
         WaitUntil(() => close.IsKeyboardFocusWithin && _dashboard.Tree.SearchText == "shell", "Tab reached the close button and kept the query");
         Press(_dashboard, Key.Tab, PhysicalKey.Tab, RawInputModifiers.Shift);
         WaitUntil(() => search.IsKeyboardFocusWithin, "Shift+Tab returned to search");
      }
   }

   [XF] public void the_worktree_context_menu_opens_Git_Extensions_for_the_clicked_checkout()
   {
      var active = OnTheUIThread(() => _dashboard.Tree.ActiveSession);
      var menu = OnTheUIThread(() =>
      {
         var rows = _dashboard.GetVisualDescendants().OfType<ListBoxItem>().ToArray();
         rows.Where(it => it.DataContext is DashboardProject or DashboardSession).All(it => it.ContextMenu is null).Must().BeTrue();
         var at = PointOf(_emptyGroup, .5);
         _dashboard.MouseDown(at, MouseButton.Right);
         _dashboard.MouseUp(at, MouseButton.Right);
         return rows.Single(it => it.DataContext == _emptyGroup).ContextMenu!;
      });
      var gitExtensions = OnTheUIThread(() => menu.Items.OfType<MenuItem>().Single());
      WaitUntil(() => menu.IsOpen && LaidOut(gitExtensions), "right-click showed the Git Extensions menu item",
                () => $"menu open {menu.IsOpen}, item bounds {gitExtensions.Bounds}", TimeSpan.FromSeconds(4));
      OnTheUIThread(() => gitExtensions.IsSubMenuOpen = true);
      WaitUntil(() => gitExtensions.Items.OfType<MenuItem>().All(LaidOut), "both Git Extensions actions were visible");
      OnTheUIThread(() =>
      {
         gitExtensions.Header.Must().Be("Git Extensions");
         var actions = gitExtensions.Items.OfType<MenuItem>().ToArray();
         actions.Select(it => it.Header).ToArray().Must().SequenceEqual(["Browse", "Commit"]);
         foreach(var action in actions)
         {
            var launch = (ProcessStartInfo)action.CommandParameter!;
            launch.WorkingDirectory.Must().Be(_empty.FullName);
            launch.FileName.Must().Be("GitExtensions.exe");
            launch.ArgumentList.Single().Must().Be(((string)action.Header!).ToLowerInvariant());
         }
         menu.Close();
         _dashboard.Tree.ActiveSession.Must().ReferenceEqual(active!);
      });

      static bool LaidOut(MenuItem item) =>
         item.IsEffectivelyVisible && TopLevel.GetTopLevel(item) is not null && item.Bounds is { Width: > 0, Height: > 0 };
   }

   [XF] public void both_agents_follow_the_worktree_where_they_run_even_when_their_host_was_started_elsewhere()
   {
      StartInAnotherCheckout(_first, "codex");
      StartInAnotherCheckout(_second, "claude");
      OnTheUIThread(() => _emptyGroup.Sessions.Select(it => it.Host!.Id).ToHashSet().SetEquals([_first, _second])).Must().BeTrue();
      var agents = OnTheUIThread(() => _emptyGroup.Sessions.Select(it => it.Latest!.Id).ToHashSet());
      Close(_dashboard);
      Close(OnTheUIThread(() => Sandbox.WindowsOfCloyd.Open.Single(it => it.Project.Equals(_alpha.Project))));
      _alpha.SavedWindow.Tabs.Select(it => it.Session?.Id).ToHashSet().SetEquals(agents).Must().BeTrue();
      _alpha.SavedWindow.Tabs.All(it => it.SessionDirectory == _empty.FullName).Must().BeTrue();
      _alpha.End(_first);
      _alpha.End(_second);
      var restored = Open(_alpha.Window);
      WaitUntil(() => restored.BringingBackTheTabs.IsCompleted && restored.Tabs.Count == 2 && restored.Tabs.All(it => it.SessionRunningInIt is not null),
                "both sessions resumed in replacement hosts",
                () => $"{restored.Tabs.Count} tabs, {restored.Failure?.Message}, {string.Join(", ", restored.Tabs.Select(it => it.Notice ?? it.Title))}");
      OnTheUIThread(() => restored.Tabs.All(it => it.Host.Directory == _empty.FullName && agents.Contains(it.LatestSession!.Id))).Must().BeTrue();

      void StartInAnotherCheckout(SessionHostId id, string agent)
      {
         var row = OnTheUIThread(() => _linkedGroup.Sessions.Single(it => it.Host!.Id == id));
         OnTheUIThread(() =>
         {
            _dashboard.Select(row);
            if(_dashboard.View!.ShowsTheChat) _dashboard.View.SwitchBetweenTheTerminalAndTheChat();
         });
         WaitUntil(() => _dashboard.View!.Terminal.TextShown.Contains("PS "), "the helper shell was ready");
         DoneOnTheUIThread(() => _dashboard.View!.Terminal.SendInputAsync($"Set-Location -LiteralPath '{_empty.FullName.Replace("'", "''")}'; {agent}\r"));
         WaitUntil(() => row.Running is not null && _emptyGroup.Sessions.Contains(row), "the running agent appeared in its actual checkout");
         DoneOnTheUIThread(() => _dashboard.View!.Terminal.SendInputAsync("Remember this worktree\r"));
         WaitUntil(() => Sandbox.HubOfTheSandbox.SessionsOnDisk().Any(it => it.Session.Id == row.Latest!.Id && it.HasAConversationToResume),
                   "the agent wrote a resumable conversation");
         OnTheUIThread(() => row.Host!.Directory).Must().Be(_linked.FullName);
         OnTheUIThread(() => row.WorkingFolder!.Folder).Must().Be(_empty.FullName);
      }
   }

   [XF] public void only_pinned_hides_every_empty_ancestor_preserves_the_open_view_and_remembers_the_filter()
   {
      var pinned = OnTheUIThread(() => _linkedGroup.Sessions.Single(it => it.Host!.Id == _first));
      OnTheUIThread(() => _dashboard.Select(_linkedGroup.Sessions.Single(it => it.Host!.Id == _second)));
      var view = OnTheUIThread(() => _dashboard.View);
      Click(PinButton(_dashboard, pinned));
      var root = OnTheUIThread(() => _dashboard.GetVisualDescendants().OfType<SessionFilterStrip>().Single(it => it.DataContext == _dashboard.Tree));
      foreach(var name in new[] { "Pinned", "Open tabs", "Sessions outside tabs" })
         Click(OnTheUIThread(() => root.GetVisualDescendants().OfType<Button>().Single(it => AutomationProperties.GetName(it) == name)));
      WaitUntil(() => (_dashboard.Tree.Filters.Effective(null) & IncludedSessions.MembershipSwitches) == IncludedSessions.Pinned, "the root switches included pinned sessions alone");
      OnTheUIThread(() => _dashboard.Tree.VisibleRows.ToArray()).Must().SequenceEqual([_alphaGroup, _linkedGroup, pinned]);
      Render(_dashboard, "Projects-Dashboard-only-pinned");
      OnTheUIThread(() => _dashboard.View).Must().ReferenceEqual(view);
      OnTheUIThread(() => _dashboard.Tree.Toggle(_alphaGroup));
      OnTheUIThread(() => _dashboard.Tree.VisibleRows.ToArray()).Must().SequenceEqual([_alphaGroup]);
      OnTheUIThread(() => _alphaGroup.Summary).Must().Be("1 session");
      OnTheUIThread(() => _dashboard.Tree.Toggle(_alphaGroup));
      Press(_dashboard, Key.Tab, PhysicalKey.Tab, RawInputModifiers.Control);
      WaitUntil(() => _dashboard.View!.Host.Id == _first, "Ctrl+Tab chose the one pinned session");
      Close(_dashboard);
      var reopened = OnTheUIThread(() => Sandbox.WindowsOfCloyd.ShowDashboard(_alpha.Project));
      WaitUntil(() => (reopened.Tree.Filters.Effective(null) & IncludedSessions.MembershipSwitches) == IncludedSessions.Pinned && reopened.Tree.VisibleRows.Count == 3 && reopened.Tree.NavigableSessions.SingleOrDefault()?.Host?.Id == _first,
                "the saved pin and filter came back beneath the linked checkout");
      Click(PinButton(reopened, OnTheUIThread(() => reopened.Tree.NavigableSessions.Single())));
      WaitUntil(() => reopened.Tree.VisibleRows.Count == 0 && reopened.Tree.NoMatches, "removing the last pin hid every group");
      OnTheUIThread(() => reopened.View!.Host.Id).Must().Be(_first);

      Button PinButton(ProjectsDashboardWindow window, DashboardSession row) => OnTheUIThread(() => window.GetVisualDescendants()
        .OfType<Button>().Single(it => it.DataContext == row && Equals(ToolTip.GetTip(it), row.PinAction)));
   }

   [XF] public void a_dashboard_saved_with_the_old_Only_pinned_checkbox_restores_only_its_pinned_session()
   {
      Close(_dashboard);
      var saved = Sandbox.WindowsOfCloyd.Sessions.Folder.ProjectsDashboard;
      saved.Write(JsonSerializer.Serialize(new { arrangement = new { onlyPinned = true, pinnedSessions = new[] { $"host:{_first}" } } }));
      var reopened = OnTheUIThread(() => Sandbox.WindowsOfCloyd.ShowDashboard(_alpha.Project));
      WaitUntil(() => (reopened.Tree.Filters.Effective(null) & IncludedSessions.MembershipSwitches) == IncludedSessions.Pinned
                      && reopened.Tree.NavigableSessions.SingleOrDefault()?.Host?.Id == _first,
                "the former checkbox became the Pinned inclusion switch");
      OnTheUIThread(() => reopened.Tree.ToggleInclusion(null, IncludedSessions.OpenTabs));
      Close(reopened);
      var changed = OnTheUIThread(() => Sandbox.WindowsOfCloyd.ShowDashboard(_alpha.Project));
      WaitUntil(() => (changed.Tree.Filters.Effective(null) & IncludedSessions.MembershipSwitches) == (IncludedSessions.Pinned | IncludedSessions.OpenTabs)
                      && changed.Tree.NavigableSessions.Count() == 2,
                "later changes to the switches took precedence over the migrated preference");
   }

   [XF] public void Claudes_trust_config_lists_dormant_projects_and_all_their_worktrees_including_inherited_trust()
   {
      var parent = Sandbox.Project("Trusted parent").Project;
      var nested = Directory.CreateDirectory(Path.Combine(parent.Folder, "Inherited project"));
      GitRepositoryOfASpecification.In(new DirectoryInfo(parent.Folder));
      var inherited = GitRepositoryOfASpecification.In(nested);
      var linked = new DirectoryInfo(Path.Combine(Sandbox.ProjectsFolder, "Distant dormant worktree"));
      inherited.HasAWorktreeIn(linked, "topic");
      var untrusted = Sandbox.Project("Untrusted").Project;
      GitRepositoryOfASpecification.In(new DirectoryInfo(untrusted.Folder));
      var config = Path.Combine(Sandbox.HubOfTheSandbox.Environment["CLAUDE_CONFIG_DIR"], ".claude.json");
      new FileWrittenWhole(config).Write(JsonSerializer.Serialize(new { projects = new Dictionary<string, object>
      {
         [parent.Folder] = new { hasTrustDialogAccepted = true },
         [nested.FullName] = new { },
         [untrusted.Folder] = new { hasTrustDialogAccepted = false }
      } }));
      Close(_dashboard);
      var reopened = OnTheUIThread(() => Sandbox.WindowsOfCloyd.ShowDashboard(_alpha.Project));
      WaitUntil(() => reopened.Tree.Projects.Any(it => it.Project?.Folder == nested.FullName && it.Children.OfType<DashboardWorktree>().Any(w => w.StartFolder.Folder == linked.FullName)),
                "the inactive inherited project and its remote worktree appeared");
      OnTheUIThread(() => reopened.Tree.Projects.Any(it => it.Project?.Equals(parent) == true)).Must().BeTrue();
      OnTheUIThread(() => reopened.Tree.Projects.Any(it => it.Project?.Equals(untrusted) == true)).Must().BeFalse();
      OnTheUIThread(() => reopened.Tree.Projects.Single(it => it.Project?.Folder == nested.FullName).Sessions.Count()).Must().Be(0);
      new FileWrittenWhole(config).Write(JsonSerializer.Serialize(new { projects = new Dictionary<string, object>
      {
         [untrusted.Folder] = new { hasTrustDialogAccepted = true }
      } }));
      WaitUntil(() => reopened.Tree.Projects.Any(it => it.Project?.Equals(untrusted) == true), "Cloyd reread Claude's changed trust configuration");
      OnTheUIThread(() => reopened.Tree.Projects.Any(it => it.Project?.Equals(parent) == true || it.Project?.Folder == nested.FullName)).Must().BeFalse();
   }

   [XF] public void a_local_inclusion_overrides_only_its_own_switch_and_can_inherit_again()
   {
      OnTheUIThread(() =>
      {
         var tree = _dashboard.Tree;
         tree.ToggleInclusion(null, IncludedSessions.OpenTabs);
         tree.ToggleInclusion(null, IncludedSessions.OutsideTabs);
         tree.ToggleInclusion(null, IncludedSessions.Pinned);
         tree.VisibleRows.Count.Must().Be(0);
         tree.ToggleInclusion(_linkedGroup, IncludedSessions.OpenTabs);
         tree.NavigableSessions.Count().Must().Be(2);
         tree.Filters.IsInherited(_linkedGroup, IncludedSessions.Pinned).Must().BeTrue();
         tree.ToggleInclusion(_alphaGroup, IncludedSessions.OpenTabs);
         tree.ToggleInclusion(_alphaGroup, IncludedSessions.OpenTabs);
         tree.NavigableSessions.Count().Must().Be(2);
      });
      Close(_dashboard);
      var reopened = OnTheUIThread(() => Sandbox.WindowsOfCloyd.ShowDashboard(_alpha.Project));
      WaitUntil(() => reopened.Tree.NavigableSessions.Count() == 2, "the worktree's local Open tabs override survived reopening");
      var linked = OnTheUIThread(() => reopened.Tree.Projects.SelectMany(it => it.Children).OfType<DashboardWorktree>()
                                             .Single(it => it.StartFolder.Folder == _linked.FullName));
      var localSwitch = OnTheUIThread(() => reopened.GetVisualDescendants().OfType<SessionFilterStrip>()
         .Single(it => it.DataContext == linked).GetVisualDescendants().OfType<Button>().Single(it => AutomationProperties.GetName(it) == "Open tabs"));
      Focus(localSwitch);
      Press(reopened, Key.Delete, PhysicalKey.Delete, RawInputModifiers.None);
      WaitUntil(() => reopened.Tree.Filters.IsInherited(linked, IncludedSessions.OpenTabs) && reopened.Tree.NoMatches,
                "Delete restored the project's explicit off setting");
      Focus(OnTheUIThread(() => reopened.GetVisualDescendants().OfType<Button>().Single(it => AutomationProperties.GetName(it) == "All groups")));
      Press(reopened, Key.Space, PhysicalKey.Space, RawInputModifiers.None);
      WaitUntil(() => reopened.Tree.VisibleRows.Contains(linked) && !reopened.Tree.VisibleRows.OfType<DashboardSession>().Any(),
                "the gear revealed the hidden worktree's switches");
   }

   [XF] public void the_worktree_clock_opens_a_filtered_out_conversation_and_only_Resume_starts_an_agent()
   {
      var saved = new SessionOfASpecification(new ClaudeCodeSessionId("9f222222-0000-4321-9876-000000000001"), _linked.FullName, "A saved worktree conversation");
      var other = new SessionOfASpecification(new ClaudeCodeSessionId("9f222222-0000-4321-9876-000000000002"), _alpha.Project.Folder, "Main conversation");
      foreach(var item in new[] { saved, other }) item.WriteItsTranscriptFor(Sandbox.HubOfTheSandbox);
      var codex = new CodexSessionOfASpecification(new CodexSessionId("9f222222-0000-4321-9876-000000000003"), _elsewhere.Project.Folder, "A different repository");
      codex.WriteItsRecordFor(Sandbox.HubOfTheSandbox);
      OnTheUIThread(() => _dashboard.Tree.ToggleInclusion(null, IncludedSessions.OutsideTabs));
      Click(OnTheUIThread(() => _dashboard.GetVisualDescendants().OfType<Button>().Single(it => AutomationProperties.GetName(it) == "All projects history")));
      WaitUntil(() => _dashboard.HistoryPicker.Listed.Count == 3, "the top history clock listed all projects");
      OnTheUIThread(() => _dashboard.HistoryPicker.Listed.Select(it => it.Id).ToHashSet().SetEquals([saved.Id, other.Id, codex.Id])).Must().BeTrue();
      OnTheUIThread(() =>
      {
         _dashboard.HistoryPicker.Selected = _dashboard.HistoryPicker.Listed.ToList().FindIndex(it => it.Id == codex.Id);
         _dashboard.HistoryPicker.ChooseTheSelected();
      });
      WaitUntil(() => _dashboard.GetVisualDescendants().OfType<Cloyd.Codex.Chat.ChatOfACodexSession>()
         .Any(it => it.Shown.OfType<Cloyd.Codex.Chat.CodexReply>().Any(reply => reply.Markdown.Contains(codex.Prompt))), "the recorded Codex conversation was readable without starting Codex");
      DoneOnTheUIThread(() => _dashboard.OpenHistory(_alphaGroup));
      OnTheUIThread(() => _dashboard.HistoryPicker.Listed.Select(it => it.Id).ToHashSet().SetEquals([saved.Id, other.Id])).Must().BeTrue();
      OnTheUIThread(_dashboard.HistoryPicker.Close);
      var button = OnTheUIThread(() => _dashboard.GetVisualDescendants().OfType<Button>()
         .Single(it => it.DataContext == _linkedGroup && AutomationProperties.GetName(it) == "Group history"));
      Click(button);
      WaitUntil(() => _dashboard.HistoryPicker.Listed.Any(it => it.Id == saved.Id), "the worktree clock found the archived conversation");
      OnTheUIThread(() => _dashboard.HistoryPicker.Listed.Select(it => it.Id).ToArray()).Must().SequenceEqual([saved.Id]);
      OnTheUIThread(_dashboard.HistoryPicker.ChooseTheSelected);
      WaitUntil(() => _dashboard.Tree.ActiveSession?.Latest?.Id == saved.Id, "the dashboard opened the archived conversation");
      WaitUntil(() => _dashboard.GetVisualDescendants().OfType<Cloyd.Chat.ChatOfAClaudeCodeSession>().Any(it =>
         it.Shown.OfType<Cloyd.Chat.ShownInTheChat.Prompt>().Any(prompt => prompt.Text == saved.Prompt)), "the recorded Claude Code conversation was visible");
      OnTheUIThread(() => _dashboard.Tree.ActiveSession!.ReadyToResume).Must().BeTrue();
      OnTheUIThread(() => _dashboard.Tree.VisibleRows.Contains(_dashboard.Tree.ActiveSession!)).Must().BeFalse();
      OnTheUIThread(() =>
      {
         var tree = _dashboard.Tree;
         tree.TogglePin(tree.ActiveSession!);
         tree.VisibleRows.Contains(tree.ActiveSession!).Must().BeFalse();
         tree.ToggleInclusion(_alphaGroup, IncludedSessions.Pinned);
         tree.NavigableSessions.Count().Must().Be(3);
         tree.ToggleInclusion(null, IncludedSessions.AllStatuses);
         tree.ToggleInclusion(null, IncludedSessions.ReadyToResume);
         tree.NavigableSessions.Select(it => it.Latest!.Id).ToArray().Must().SequenceEqual([saved.Id]);
      });
      Sandbox.HubOfTheSandbox.RunningClaudeCodes().Any(it => it.SessionId == saved.Id).Must().BeFalse();
      DoneOnTheUIThread(_dashboard.ResumeSelected);
      WaitUntil(() => _dashboard.View?.Session.Running?.Session.Id == saved.Id, "Resume started the original session");
      OnTheUIThread(() => _dashboard.View!.Host.Directory).Must().Be(_linked.FullName);
   }

   [XF] public void the_worktree_and_its_sessions_belong_to_the_repository_that_registered_it()
   {
      OnTheUIThread(() => _linkedGroup.Sessions.Select(it => it.Host!.Id).ToHashSet().SetEquals([_first, _second])).Must().BeTrue();
      OnTheUIThread(() => _elsewhereGroup.Sessions.Count()).Must().Be(0);
      OnTheUIThread(() => _emptyGroup.Sessions.Count()).Must().Be(0);
      OnTheUIThread(() => _emptyGroup.CanStartSession).Must().BeTrue();
      WaitUntil(() => _linkedGroup.Status?.Integrated == true, "the linked branch icon reported its commits were merged into the local integration branch");
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
