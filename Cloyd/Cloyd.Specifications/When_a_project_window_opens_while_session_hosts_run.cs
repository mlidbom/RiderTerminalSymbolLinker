using ClaudeTooling.SessionHost.Protocol;
using Compze.Must;
using Compze.xUnitBDD;
using Cloyd.Hub;
using Cloyd.Projects;
using Cloyd.SessionHosts;
using Cloyd.Specifications.TheSandbox;

namespace Cloyd.Specifications;

public class When_a_project_window_opens_while_session_hosts_run : SpecificationOpeningWindows
{
   readonly SessionHostOfTheHub _startedByAnEarlierWindow, _startedByAClaudeSessionsTab, _shownByAClaudeSessionsTab, _ofAnotherProject, _ofAnotherTool;
   readonly ProjectWindow _window;

   public When_a_project_window_opens_while_session_hosts_run() =>
      (_startedByAnEarlierWindow, _startedByAClaudeSessionsTab, _shownByAClaudeSessionsTab, _ofAnotherProject, _ofAnotherTool, _window) = Once(() =>
      {
         var project = EndWithTheScenario(SandboxProject.Named(nameof(When_a_project_window_opens_while_session_hosts_run)));
         var anotherProject = EndWithTheScenario(SandboxProject.Named(nameof(When_a_project_window_opens_while_session_hosts_run) + "-another"));
         var startedByAnEarlierWindow = project.StartASessionHost(project.LabelOfCloydsHosts);
         var startedByAClaudeSessionsTab = project.StartASessionHost(project.LabelOfAClaudeSessionsTab);
         var shownByAClaudeSessionsTab = project.StartASessionHost(project.LabelOfAClaudeSessionsTab);
         EndWithTheScenario(AttachmentToASessionHost.To(shownByAClaudeSessionsTab.Id, ConsoleSize.Default));
         var ofAnotherProject = project.StartASessionHost(anotherProject.LabelOfCloydsHosts);
         var ofAnotherTool = project.StartASessionHost("""{"someTool":"its own"}""");
         var window = Open(project.Window);
         WaitUntil(() => window.BringingBackTheTabs.IsCompleted, "the window had brought back its tabs");
         return (startedByAnEarlierWindow, startedByAClaudeSessionsTab, shownByAClaudeSessionsTab, ofAnotherProject, ofAnotherTool, window);
      });

   SessionHostId[] HostsShown => OnTheUIThread(() => _window.Tabs.Select(it => it.Host.Id).ToArray());

   [XF] public void it_shows_the_host_an_earlier_window_of_the_project_started() => HostsShown.Must().Satisfy(it => it.Contains(_startedByAnEarlierWindow.Id));

   [XF] public void it_shows_the_host_a_Claude_Sessions_tab_started_for_the_project_once_no_tab_shows_it() => HostsShown.Must().Satisfy(it => it.Contains(_startedByAClaudeSessionsTab.Id));

   [XF] public void it_leaves_the_host_a_Claude_Sessions_tab_shows_to_the_tab() => HostsShown.Must().Satisfy(it => !it.Contains(_shownByAClaudeSessionsTab.Id));

   [XF] public void it_leaves_the_host_of_another_project_alone() => HostsShown.Must().Satisfy(it => !it.Contains(_ofAnotherProject.Id));

   [XF] public void it_leaves_the_host_of_another_tool_alone() => HostsShown.Must().Satisfy(it => !it.Contains(_ofAnotherTool.Id));

   [XF] public void it_shows_no_other_host() => HostsShown.Must().HaveCount(2);

   [XF] public void it_selects_the_first_tab() => OnTheUIThread(() => _window.SelectedTab?.Host.Id).Must().Be(_startedByAnEarlierWindow.Id);
}
