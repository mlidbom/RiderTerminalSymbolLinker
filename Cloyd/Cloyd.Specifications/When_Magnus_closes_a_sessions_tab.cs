using ClaudeTooling.SessionHost.Protocol;
using Compze.Must;
using Compze.xUnitBDD;
using Cloyd.Projects;
using Cloyd.Sessions;
using Cloyd.Specifications.TheSandbox;

namespace Cloyd.Specifications;

public class When_Magnus_closes_a_sessions_tab : SpecificationOpeningWindows
{
   readonly SandboxProject _project;
   readonly ProjectWindow _window;
   readonly SessionHostId _host;

   public When_Magnus_closes_a_sessions_tab() => (_project, _window, _host) = Once(() =>
   {
      var project = Sandbox.Project("the-project");
      var window = Open(project.Window);
      DoneOnTheUIThread(() => window.StartANewSession(new NewSession(AgentCli.ClaudeCode, "to be closed")));
      var tab = OnTheUIThread(() => window.Tabs.Single());
      WaitUntil(() => tab.SessionRunningInIt is not null, "Claude Code ran in the tab's host");
      Click(TheButtonWithTheTip(tab, "End the session"));
      return (project, window, tab.Host.Id);
   });

   [XF] public void the_tab_is_gone() => OnTheUIThread(() => _window.Tabs).Must().BeEmpty();

   [XF] public void its_session_ends_with_its_host()
   {
      WaitUntil(() => _project.SessionHost(_host) is null, "the hub no longer listed the host");
      _project.SessionRunningIn(_host).Must().BeNull();
   }

   [XF] public void the_window_tells_of_no_failure()
   {
      WaitUntil(() => _project.SessionHost(_host) is null, "the hub no longer listed the host");
      OnTheUIThread(() => _window.Failure).Must().BeNull();
   }
}
