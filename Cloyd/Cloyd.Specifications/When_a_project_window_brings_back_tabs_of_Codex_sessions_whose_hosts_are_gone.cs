using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Codex;
using Cloyd.Hub;
using Cloyd.Projects;
using Cloyd.Saving;
using Cloyd.Sessions;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

/// <summary>
/// As after a reboot: a project window opens with two saved tabs of Codex sessions, whose hosts are gone. Codex has the
/// first session on disk, and not the second, as after Magnus archived it. Drawn to
/// <c>RenderedWindows/A-Codex-session-resumed-after-its-host-ended.png</c>.
/// </summary>
public class When_a_project_window_brings_back_tabs_of_Codex_sessions_whose_hosts_are_gone : SpecificationOpeningWindows
{
   static readonly CodexSession OnDisk = new(new CodexSessionId("019a0003-c285-7962-a6ae-85f69a3a51f3"), "Porting the parser");
   static readonly CodexSession NotOnDisk = new(new CodexSessionId("019a0004-c285-7962-a6ae-85f69a3a51f3"), "Archived work");

   readonly SandboxProject _project;
   readonly ProjectWindow _window;
   readonly SessionTab _tab;

   public When_a_project_window_brings_back_tabs_of_Codex_sessions_whose_hosts_are_gone() => (_project, _window, _tab) = Once(() =>
   {
      var project = Sandbox.Project("the-project");
      var folder = project.Project.Folder;
      new CodexSessionOfASpecification(OnDisk.Id, folder, "Port the parser to C#") { Name = OnDisk.Name }.WriteItsRecordFor(Sandbox.HubOfTheSandbox);
      var goneHost = new SessionHostId("0000000000000001");
      project.SaveTheWindowAs(new SavedProjectWindow([new SavedSessionTab(goneHost, folder, OnDisk), new SavedSessionTab(new SessionHostId("0000000000000002"), folder, NotOnDisk)], goneHost, Desktop: null));
      var window = Open(project.Window);
      WaitUntil(() => window.BringingBackTheTabs.IsCompleted, "the window had brought back its tabs");
      var tab = OnTheUIThread(() => window.Tabs.Single());
      WaitUntil(() => tab.SessionRunningInIt is not null, "the hub told the tab of the Codex in its new host");
      Render(window, "A-Codex-session-resumed-after-its-host-ended");
      return (project, window, tab);
   });

   [XF] public void it_brings_back_only_the_tab_whose_session_Codex_has_on_disk() => OnTheUIThread(() => _window.Tabs.Select(it => it.LatestSession).ToArray()).Must().SequenceEqual<Session?>([OnDisk]);

   [XF] public void its_host_is_new_and_the_projects() => _project.SessionHost(_tab.Host.Id)!.Directory.Must().Be(_project.Project.Folder);

   [XF] public void Codex_resumes_its_session_in_it() =>
      OnTheUIThread(() => _tab.SessionRunningInIt).Must().Satisfy(it => it is RunningCodex codex && codex.Session.Id == OnDisk.Id);

   [XF] public void the_tab_is_titled_with_the_sessions_name() => OnTheUIThread(() => _tab.Title).Must().Be("Porting the parser");

   [XF] public void it_tells_of_no_failure() => OnTheUIThread(() => _window.Failure).Must().BeNull();
}
