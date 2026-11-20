using ClaudeTooling.SessionHost.Protocol;
using Compze.Must;
using Compze.xUnitBDD;
using Cloyd.Projects;
using Cloyd.Specifications.TheSandbox;

namespace Cloyd.Specifications;

public class When_Magnus_closes_a_sessions_tab : SpecificationOpeningWindows
{
   readonly SandboxProject _project = SandboxProject.Named(nameof(When_Magnus_closes_a_sessions_tab));
   readonly ProjectWindow _window;
   readonly SessionHostId _host;

   public When_Magnus_closes_a_sessions_tab()
   {
      _window = Open(_project.Window);
      DoneOnTheUIThread(() => _window.StartANewSession("to be closed"));
      var tab = OnTheUIThread(() => _window.Tabs.Single());
      _host = tab.Host.Id;
      WaitUntil(() => tab.SessionRunningInIt is not null, "Claude Code ran in the tab's host");
      Click(TheButtonWithTheTip(tab, "End the session"));
   }

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

   public override void Dispose()
   {
      base.Dispose();
      _project.Dispose();
   }
}
