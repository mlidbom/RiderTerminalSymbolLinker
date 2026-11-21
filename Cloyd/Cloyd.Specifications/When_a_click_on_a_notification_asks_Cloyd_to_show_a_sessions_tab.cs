using System.Net;
using Compze.Must;
using Compze.xUnitBDD;
using Cloyd.Hub;
using Cloyd.LocalApi.Contract;
using Cloyd.Projects;
using Cloyd.Sessions;
using Cloyd.Specifications.TheSandbox;

namespace Cloyd.Specifications;

/// <summary>
/// A project's window shows two sessions, the second selected, and Magnus clicks a notification of the first, which has
/// the hub ask the Cloyd application's local API to show its tab: <c>POST /sessions/show</c>, naming the session, its
/// Claude Code's process, or both.
/// </summary>
public class When_a_click_on_a_notification_asks_Cloyd_to_show_a_sessions_tab : SpecificationRunningTheCloydApplication
{
   readonly SandboxProject _project;
   readonly ProjectWindow _window;
   readonly SessionTab _first;
   readonly SessionTab _second;
   readonly RunningClaudeCode _firstsClaudeCode;

   public When_a_click_on_a_notification_asks_Cloyd_to_show_a_sessions_tab()
   {
      _project = Sandbox.Project("the-project");
      HandedOver(_project);
      _window = OnTheUIThread(() => Windows.Open.Single());
      DoneOnTheUIThread(() => _window.StartANewSession(new NewSession(AgentCli.ClaudeCode, "First")));
      DoneOnTheUIThread(() => _window.StartANewSession(new NewSession(AgentCli.ClaudeCode, "Second")));
      (_first, _second) = OnTheUIThread(() => (_window.Tabs[0], _window.Tabs[1]));
      WaitUntil(() => _first.SessionRunningInIt is not null && _second.SessionRunningInIt is not null, "the hub told each tab of the Claude Code in its host");
      _firstsClaudeCode = OnTheUIThread(() => (RunningClaudeCode)_first.SessionRunningInIt!);
   }

   private protected override IReadOnlyList<SandboxProject> Projects => [_project];

   SessionTab SelectedTab => OnTheUIThread(() => _window.SelectedTab!);

   HttpResponseMessage AskedToShow(string query) => Posted($"sessions/show?{query}");

   /// <summary>Has the first tab's Claude Code run <paramref name="command"/>, as Magnus types it, and waits until <paramref name="done"/>.</summary>
   void TheFirstTabsClaudeCodeRuns(string command, Func<bool> done)
   {
      DoneOnTheUIThread(() => _first.Terminal.SendInputAsync($"{command}\r"));
      WaitUntil(done, $"the hub told the first tab of what {command} did");
   }

   public class and_it_names_the_session : When_a_click_on_a_notification_asks_Cloyd_to_show_a_sessions_tab
   {
      readonly HttpResponseMessage _answer;

      public and_it_names_the_session() => _answer = AskedToShow($"session={_firstsClaudeCode.SessionId}");

      [XF] public void it_answers_with_the_window_it_brought_forward() =>
         _answer.Content.ReadFromJsonAsync<WindowInTheAnswer>().GetAwaiter().GetResult().Must().Be(new WindowInTheAnswer(_project.Project.Folder, $"Cloyd: {_project.Project.Name}"));

      [XF] public void it_answers_200() => _answer.StatusCode.Must().Be(HttpStatusCode.OK);

      [XF] public void it_selects_the_sessions_tab() => SelectedTab.Must().ReferenceEqual(_first);
   }

   public class and_it_names_the_process_of_the_Claude_Code_that_has_gone_on_in_a_new_session_after_a_clear : When_a_click_on_a_notification_asks_Cloyd_to_show_a_sessions_tab
   {
      readonly HttpResponseMessage _answer;

      public and_it_names_the_process_of_the_Claude_Code_that_has_gone_on_in_a_new_session_after_a_clear()
      {
         TheFirstTabsClaudeCodeRuns("/clear", () => _first.SessionRunningInIt?.Session.Id is {} session && session != _firstsClaudeCode.SessionId);
         _answer = AskedToShow($"session={_firstsClaudeCode.SessionId}&process={_firstsClaudeCode.Process}");
      }

      [XF] public void it_answers_200() => _answer.StatusCode.Must().Be(HttpStatusCode.OK);

      [XF] public void it_selects_the_tab_the_Claude_Code_runs_in() => SelectedTab.Must().ReferenceEqual(_first);
   }

   public class and_it_names_the_session_a_tab_ran_last_before_its_Claude_Code_exited_to_the_shell : When_a_click_on_a_notification_asks_Cloyd_to_show_a_sessions_tab
   {
      readonly HttpResponseMessage _answer;

      public and_it_names_the_session_a_tab_ran_last_before_its_Claude_Code_exited_to_the_shell()
      {
         TheFirstTabsClaudeCodeRuns("/exit", () => _first.SessionRunningInIt is null);
         _answer = AskedToShow($"session={_firstsClaudeCode.SessionId}&process={_firstsClaudeCode.Process}");
      }

      [XF] public void it_answers_200() => _answer.StatusCode.Must().Be(HttpStatusCode.OK);

      [XF] public void it_selects_the_tab_that_ran_it() => SelectedTab.Must().ReferenceEqual(_first);
   }

   public class and_it_names_a_session_no_tab_runs_or_ran : When_a_click_on_a_notification_asks_Cloyd_to_show_a_sessions_tab
   {
      readonly HttpResponseMessage _answer;

      public and_it_names_a_session_no_tab_runs_or_ran() => _answer = AskedToShow($"session={Guid.NewGuid()}");

      [XF] public void it_answers_404() => _answer.StatusCode.Must().Be(HttpStatusCode.NotFound);

      [XF] public void the_selected_tab_stays_selected() => SelectedTab.Must().ReferenceEqual(_second);
   }

   public class and_it_names_neither_a_session_nor_a_process : When_a_click_on_a_notification_asks_Cloyd_to_show_a_sessions_tab
   {
      readonly HttpResponseMessage _answer;

      public and_it_names_neither_a_session_nor_a_process() => _answer = AskedToShow("");

      [XF] public void it_answers_400() => _answer.StatusCode.Must().Be(HttpStatusCode.BadRequest);
   }
}
