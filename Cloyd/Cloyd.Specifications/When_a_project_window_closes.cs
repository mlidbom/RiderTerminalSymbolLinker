using ClaudeTooling.SessionHost.Protocol;
using Compze.Must;
using Compze.xUnitBDD;
using Cloyd.Projects;
using Cloyd.Specifications.TheSandbox;

namespace Cloyd.Specifications;

public class When_a_project_window_closes : SpecificationOpeningWindows
{
   readonly SandboxProject _project;
   readonly SessionHostId _host;

   public When_a_project_window_closes() => (_project, _host) = Once(() =>
   {
      var project = Sandbox.Project("the-project");
      var window = Open(project.Window);
      DoneOnTheUIThread(() => window.StartANewSession(null));
      var tab = OnTheUIThread(() => window.Tabs.Single());
      var host = tab.Host.Id;
      WaitUntil(() => tab.SessionRunningInIt is not null, "Claude Code ran in the tab's host");
      Close(window);
      WaitUntil(() => project.SessionHost(host) is { Attached: false }, "the hub listed the host as shown by no attach client");
      return (project, host);
   });

   [XF] public void its_sessions_run_on() => _project.SessionRunningIn(_host).Must().NotBeNull();

   public class and_the_project_window_opens_again : When_a_project_window_closes
   {
      readonly ProjectWindow _windowAgain;

      public and_the_project_window_opens_again() => _windowAgain = Once(() =>
      {
         var windowAgain = Open(_project.Window);
         WaitUntil(() => windowAgain.BringingBackTheTabs.IsCompleted, "the window had brought back its tabs");
         WaitUntil(() => windowAgain.Tabs.Single().Terminal.TextShown.Contains("New session"), "the tab showed the session's screen again");
         Render(windowAgain, "A-project-window-opened-again");
         return windowAgain;
      });

      [XF] public void it_shows_the_session_again_in_a_tab() => OnTheUIThread(() => _windowAgain.Tabs.Single().Host.Id).Must().Be(_host);

      [XF] public void the_tab_is_attached_to_the_host() => _project.SessionHost(_host)!.Attached.Must().BeTrue();

      [XF] public void the_tab_is_titled_with_the_name_the_hub_tells_of_the_session()
      {
         WaitUntil(() => _windowAgain.Tabs.Single().SessionRunningInIt is not null, "the hub told the tab of the Claude Code in its host");
         var name = _project.SessionRunningIn(_host)!.Session.Name;
         name.Must().NotBeNullOrEmpty();
         OnTheUIThread(() => _windowAgain.Tabs.Single().Title).Must().Be(name!);
      }
   }
}
