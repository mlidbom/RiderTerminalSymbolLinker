using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Codex;
using Cloyd.LocalApi;
using Cloyd.ProjectsDashboard;
using Cloyd.ProjectsDashboard.Filters;
using Cloyd.Saving;
using Cloyd.SessionHosts;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

public class When_Cloyd_is_launched_without_arguments : SpecificationRunningTheCloydApplication
{
   readonly SandboxProject _project;
   private protected override IReadOnlyList<SandboxProject> Projects => [_project];

   public When_Cloyd_is_launched_without_arguments() => _project = Sandbox.Project("The project to open");

   [XF] public void the_dashboard_lists_exactly_the_tabs_Open_project_will_restore()
   {
      var hub = Sandbox.HubOfTheSandbox;
      var folder = _project.Project.Folder;
      GitRepositoryOfASpecification.In(new DirectoryInfo(folder));
      var saved = new CodexSession(new CodexSessionId(Guid.NewGuid().ToString()), "Saved tab");
      var historyOnly = new CodexSessionId(Guid.NewGuid().ToString());
      var archived = new CodexSession(new CodexSessionId(Guid.NewGuid().ToString()), "Archived tab");
      new CodexSessionOfASpecification(saved.Id, folder, "Saved tab").WriteItsRecordFor(hub);
      new CodexSessionOfASpecification(historyOnly, folder, "History only").WriteItsRecordFor(hub);
      var gone = new SessionHostId("0000000000000011");
      _project.SaveTheWindowAs(new SavedProjectWindow([new(gone, folder, saved), new(new SessionHostId("0000000000000012"), folder, archived),
                                                      new(new SessionHostId("0000000000000013"), folder, saved)], gone, null));
      var live = _project.StartASessionHost(_project.LabelOfCloydsHosts);

      LaunchFromAnUnrelatedDirectory();
      var dashboard = OnTheUIThread(() => Windows.Dashboard!);
      LaunchFromAnUnrelatedDirectory();
      OnTheUIThread(() => Windows.Dashboard).Must().ReferenceEqual(dashboard);
      WindowsItLists().Must().BeEmpty();
      Answered(() => new TheCloydApplication(CloydsFolder).WindowInventory()!.DashboardOpen, "the API reported the dashboard").Must().BeTrue();
      WaitUntil(() => dashboard.Tree.Sessions.Any(it => it.Latest?.Id == historyOnly), "the dashboard read the conversation history");
      OnTheUIThread(() => dashboard.Tree.ToggleInclusion(null, IncludedSessions.OpenTabs));
      WaitUntil(() => dashboard.Tree.NavigableSessions.Count() == 2 &&
                      dashboard.Tree.NavigableSessions.Any(it => it.Host?.Id == live.Id) &&
                      dashboard.Tree.NavigableSessions.Any(it => it.Latest?.Id == saved.Id && it.Host is null), "Open tabs included the live shell and the saved tab");
      OnTheUIThread(() => dashboard.Tree.Sessions.Any(it => it.Latest?.Id == archived.Id)).Must().BeFalse();
      _project.ItsSessionHosts().Select(it => it.Id).Must().SequenceEqual([live.Id]);
      OnTheUIThread(() =>
      {
         dashboard.Tree.ToggleInclusion(null, IncludedSessions.OutsideTabs);
         dashboard.Tree.ToggleInclusion(null, IncludedSessions.OpenTabs);
         dashboard.Tree.NavigableSessions.Select(it => it.Latest!.Id).Must().SequenceEqual([historyOnly]);
         dashboard.Tree.ToggleInclusion(null, IncludedSessions.OpenTabs);
         dashboard.Tree.ToggleInclusion(null, IncludedSessions.OutsideTabs);
      });

      OpenProjectMenu(dashboard);
      WaitUntil(() => Windows.Open.Count == 1 && Windows.Open[0].BringingBackTheTabs.IsCompleted, "Open project restored the real project window");
      var project = OnTheUIThread(() => Windows.Open.Single());
      WaitUntil(() => project.SelectedTab?.SessionRunningInIt?.Session.Id == saved.Id, "the saved selection resumed only after opening its window");
      OnTheUIThread(() => project.Tabs.Count).Must().Be(2);
      OnTheUIThread(() => project.Tabs.Any(it => it.Host.Id == live.Id)).Must().BeTrue();
      OpenProjectMenu(dashboard);
      OnTheUIThread(() => Windows.Open.Single()).Must().ReferenceEqual(project);
      Close(project);
      WaitUntil(() => Windows.Open.Count == 0 && dashboard.Tree.NavigableSessions.Count() == 2, "closing the project kept both sessions in Open tabs");
   }

   void LaunchFromAnUnrelatedDirectory()
   {
      var executable = Path.Combine(AppContext.BaseDirectory, "Cloyd.exe");
      var start = new ProcessStartInfo(executable) { WorkingDirectory = Sandbox.ProjectsFolder, UseShellExecute = false };
      start.Environment["CLOYD_FOLDER"] = CloydsFolder.Folder.FullName;
      using var command = Process.Start(start)!;
      var finished = command.WaitForExitAsync();
      WaitUntil(() => finished.IsCompleted, "the no-argument launcher opened Projects Dashboard");
      command.ExitCode.Must().Be(0);
   }

   [XF] public void saved_unlabelled_hosts_count_as_tabs_and_external_attachments_are_left_alone()
   {
      var saved = _project.StartASessionHost("{}");
      var unrelated = _project.StartASessionHost("{}");
      var attached = _project.StartASessionHost(_project.LabelOfCloydsHosts);
      using var external = AttachmentToASessionHost.To(attached.Id, ConsoleSize.Default);
      var folder = _project.Project.Folder;
      _project.SaveTheWindowAs(new SavedProjectWindow([new(saved.Id, folder, null), new(attached.Id, folder, null)], saved.Id, null));
      LaunchFromAnUnrelatedDirectory();
      var dashboard = OnTheUIThread(() => Windows.Dashboard!);
      OnTheUIThread(() => dashboard.Tree.ToggleInclusion(null, IncludedSessions.OpenTabs));
      WaitUntil(() => dashboard.Tree.Sessions.Count() == 3 && dashboard.Tree.NavigableSessions.Select(it => it.Host?.Id).SequenceEqual([saved.Id]),
                "only the saved unlabelled host counted as a project tab");
      _project.SessionHost(attached.Id)!.Attached.Must().BeTrue();
      _project.SessionHost(unrelated.Id)!.Attached.Must().BeFalse();
      WindowsItLists().Must().BeEmpty();
   }

   [XF] public void a_saved_session_now_running_in_an_external_terminal_stays_outside_tabs()
   {
      var hub = Sandbox.HubOfTheSandbox;
      var folder = _project.Project.Folder;
      using var outside = ClaudeCodeInATerminalOfItsOwn.Running(hub, folder, "Outside Cloyd", "Keep this session here");
      var process = outside.Process;
      WaitUntil(() => hub.RunningClaudeCodes().Any(it => it.Process == process), "the stand-in was running outside Cloyd");
      var session = hub.RunningClaudeCodes().Single(it => it.Process == process).Session;
      var gone = new SessionHostId("0000000000000077");
      _project.SaveTheWindowAs(new SavedProjectWindow([new(gone, folder, session)], gone, null));
      LaunchFromAnUnrelatedDirectory();
      var dashboard = OnTheUIThread(() => Windows.Dashboard!);
      WaitUntil(() => dashboard.Tree.Sessions.Any(it => it.Latest?.Id == session.Id), "the external session appeared");
      OnTheUIThread(() => dashboard.Tree.ToggleInclusion(null, IncludedSessions.OpenTabs));
      HandedOver(_project);
      WaitUntil(() => Windows.Open.Single().BringingBackTheTabs.IsCompleted, "the project finished checking its saved tab");
      OnTheUIThread(() => Windows.Open.Single().Tabs.Count).Must().Be(0);
      _project.ItsSessionHosts().Count.Must().Be(0);
      OnTheUIThread(() => dashboard.Tree.NavigableSessions.Any()).Must().BeFalse();
   }

   [XF] public void two_projects_opening_together_resume_a_shared_saved_session_only_once()
   {
      var other = Sandbox.Project("Other project");
      var folder = _project.Project.Folder;
      var saved = new CodexSession(new CodexSessionId(Guid.NewGuid().ToString()), "Shared saved tab");
      new CodexSessionOfASpecification(saved.Id, folder, "One agent").WriteItsRecordFor(Sandbox.HubOfTheSandbox);
      var first = new SessionHostId("0000000000000001");
      var second = new SessionHostId("0000000000000002");
      _project.SaveTheWindowAs(new SavedProjectWindow([new(first, folder, saved)], first, null));
      other.SaveTheWindowAs(new SavedProjectWindow([new(second, folder, saved)], second, null));
      OnTheUIThread(() => { _project.Window(Windows).Show(); other.Window(Windows).Show(); });
      WaitUntil(() => Windows.Open.Count == 2 && Windows.Open.All(it => it.BringingBackTheTabs.IsCompleted), "both projects finished restoring");
      WaitUntil(() => Windows.Open.SelectMany(it => it.Tabs).Any(it => it.SessionRunningInIt?.Session.Id == saved.Id), "the recorded agent resumed");
      OnTheUIThread(() => Windows.Open.Sum(it => it.Tabs.Count)).Must().Be(1);
      _project.ItsSessionHosts().Count.Must().Be(1);
   }

   void OpenProjectMenu(ProjectsDashboardWindow dashboard)
   {
      OnTheUIThread(dashboard.BringForward);
      WaitUntil(() => Row() is { Bounds.Height: > 0 }, "the dashboard displayed the project heading",
                () => string.Join(", ", dashboard.Tree.VisibleRows.Select(it => it.Key)));
      var menu = OnTheUIThread(() =>
      {
         var row = Row()!;
         var at = row.TranslatePoint(new Point(50, row.Bounds.Height / 2), dashboard)!.Value;
         dashboard.MouseDown(at, MouseButton.Right);
         dashboard.MouseUp(at, MouseButton.Right);
         return row.ContextMenu!;
      });
      var open = OnTheUIThread(() => menu.Items.OfType<MenuItem>().Single(it => Equals(it.Header, "Open project")));
      WaitUntil(() => menu.IsOpen && open.Bounds.Height > 0, "the project context menu showed Open project");
      OnTheUIThread(() => { open.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); menu.Close(); });

      ListBoxItem? Row() => dashboard.GetVisualDescendants().OfType<ListBoxItem>().SingleOrDefault(it => it.DataContext is DashboardProject group && _project.Project.Equals(group.Project));
   }

   public override void Dispose()
   {
      OnTheUIThread(() => Windows.Dashboard?.Close());
      base.Dispose();
   }
}
