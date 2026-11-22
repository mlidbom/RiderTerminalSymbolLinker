using Compze.Must;
using Compze.xUnitBDD;
using Cloyd.Hub.RunningSessions;
using Cloyd.Projects;
using Cloyd.Sessions;
using Cloyd.Specifications.TheSandbox;

namespace Cloyd.Specifications;

/// <summary>
/// Magnus starts a new Codex session, named, in a project window, as the <see cref="NewSessionButton"/> asks for one
/// with Codex chosen: the hub types <c>codex</c> into a new host's shell, and names the session once Codex shows it.
/// Drawn to <c>RenderedWindows/A-new-Codex-session-started.png</c>.
/// </summary>
public class When_a_new_Codex_session_is_started_in_a_project_window : SpecificationOpeningWindows
{
   const string NameGiven = "Porting the parser";

   private protected readonly SandboxProject _project;
   private protected readonly ProjectWindow _window;
   private protected readonly SessionTab _tab;

   public When_a_new_Codex_session_is_started_in_a_project_window() => (_project, _window, _tab) = Once(() =>
   {
      var project = Sandbox.Project("the-project");
      var window = Open(project.Window);
      DoneOnTheUIThread(() => window.StartANewSession(new NewSession(AgentCli.Codex, NameGiven)));
      var tab = OnTheUIThread(() => window.Tabs.Single());
      WaitUntil(() => tab.SessionRunningInIt?.Session.Name == NameGiven, "the hub told the tab of the Codex in its host, which it named");
      Render(window, "A-new-Codex-session-started");
      return (project, window, tab);
   });

   [XF] public void the_window_shows_it_in_a_tab_of_its_own_which_is_selected() => OnTheUIThread(() => _window.SelectedTab).Must().ReferenceEqual(_tab);

   [XF] public void its_shell_started_in_the_projects_folder() => _project.SessionHost(_tab.Host.Id)!.Directory.Must().Be(_project.Project.Folder);

   [XF] public void the_hub_lists_Codex_running_in_its_host() => OnTheUIThread(() => _tab.SessionRunningInIt).Must().Satisfy(it => it is RunningCodex);

   [XF] public void the_tabs_agent_CLI_is_Codex() => OnTheUIThread(() => _tab.AgentCli).Must().ReferenceEqual(AgentCli.Codex);

   [XF] public void the_tab_is_titled_with_the_sessions_name() => OnTheUIThread(() => _tab.Title).Must().Be(NameGiven);

   [XF] public void the_tab_opens_in_Chat() => OnTheUIThread(() => _tab.ShowsTheChat && _tab.OffersTheChat).Must().BeTrue();

   [XF] public void the_tabs_dot_is_still() => OnTheUIThread(() => _tab.ShowsThatItsSessionWorks).Must().BeFalse();

   public class and_Codex_works_on_a_prompt : When_a_new_Codex_session_is_started_in_a_project_window
   {
      readonly bool _dotPulsedWhileItWorked;

      public and_Codex_works_on_a_prompt() => _dotPulsedWhileItWorked = Once(() =>
      {
         DoneOnTheUIThread(() => _tab.Terminal.SendInputAsync("Be busy for 3 seconds\r"));
         WaitUntil(() => _tab.ShowsThatItsSessionWorks, "the tab's dot pulsed while Codex worked");
         WaitUntil(() => !_tab.ShowsThatItsSessionWorks, "the tab's dot was still once Codex was done");
         return true;
      });

      [XF] public void the_tabs_dot_pulses_while_Codex_works_and_stops_once_it_is_done() => _dotPulsedWhileItWorked.Must().BeTrue();
   }
}
