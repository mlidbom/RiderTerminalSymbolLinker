using Avalonia.Input;
using Cloyd.ClaudeCode;
using Cloyd.Codex;
using Cloyd.History;
using Cloyd.Hub.RunningSessions;
using Cloyd.Projects;
using Cloyd.Sessions;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.History;

/// <summary>
/// Magnus opens the session history picker of a project window whose project has sessions on disk of both agent CLIs the
/// hub serves: two Claude Code sessions and two Codex sessions, used one after another, each kind named and not. None of
/// them runs. Drawn to <c>RenderedWindows/the-session-history-picker-of-Claude-Code-and-Codex.png</c>.
/// </summary>
public class When_Magnus_opens_the_session_history_picker_on_sessions_of_Claude_Code_and_Codex : SpecificationOpeningWindows
{
   private protected readonly ProjectWindow _window;
   private protected readonly SessionHistoryPicker _picker;

   private protected static readonly CodexSessionId NamedCodexs = new("019a0001-c285-7962-a6ae-85f69a3a51f3");
   private protected static readonly ClaudeCodeSessionId TitledClaudeCodes = new("8f1c0011-6c1e-4a51-9d0b-3a1f00000000");
   private protected static readonly CodexSessionId UnnamedCodexs = new("019a0002-c285-7962-a6ae-85f69a3a51f3");
   private protected static readonly ClaudeCodeSessionId UntitledClaudeCodes = new("8f1c0012-6c1e-4a51-9d0b-3a1f00000000");

   public When_Magnus_opens_the_session_history_picker_on_sessions_of_Claude_Code_and_Codex() =>
      (_window, _picker) = Once(() =>
      {
         var project = Sandbox.Project("the-project");
         var folder = project.Project.Folder;
         new CodexSessionOfASpecification(NamedCodexs, folder, "Port the parser to C#") { Name = "Porting the parser", LastUsedAgo = TimeSpan.FromMinutes(30) }.WriteItsRecordFor(Sandbox.HubOfTheSandbox);
         new SessionOfASpecification(TitledClaudeCodes, folder, "Fix the login bug") { Title = "Fixing the login", LastUsedAgo = TimeSpan.FromHours(1) }.WriteItsTranscriptFor(Sandbox.HubOfTheSandbox);
         new CodexSessionOfASpecification(UnnamedCodexs, folder, "Explain the build") { LastUsedAgo = TimeSpan.FromHours(2) }.WriteItsRecordFor(Sandbox.HubOfTheSandbox);
         new SessionOfASpecification(UntitledClaudeCodes, folder, "Tidy the build script") { LastUsedAgo = TimeSpan.FromHours(3) }.WriteItsTranscriptFor(Sandbox.HubOfTheSandbox);

         var window = Open(project.Window);
         WaitUntil(() => window.BringingBackTheTabs.IsCompleted, "the window had brought back its tabs");
         var picker = OnTheUIThread(() => window.HistoryPicker);
         Click(TheButtonWithTheTip(window, ProjectWindow.TipOfTheHistoryButton));
         WaitUntil(() => picker.Scopes.Count > 0, "the picker showed the history");
         Render(window, "the-session-history-picker-of-Claude-Code-and-Codex");
         return (window, picker);
      });

   private protected IReadOnlyList<SessionId> Listed => OnTheUIThread(() => _picker.Listed.Select(it => it.Id).ToList());

   [XF] public void it_lists_the_sessions_of_both_in_one_list_most_recently_used_first() =>
      Listed.Must().Satisfy(it => it.SequenceEqual<SessionId>([NamedCodexs, TitledClaudeCodes, UnnamedCodexs, UntitledClaudeCodes]));

   [XF] public void each_entry_is_of_its_agent_CLI() =>
      OnTheUIThread(() => _picker.Listed.Select(it => it.AgentCli).ToList()).Must().Satisfy(it => it.SequenceEqual([AgentCli.Codex, AgentCli.ClaudeCode, AgentCli.Codex, AgentCli.ClaudeCode]));

   [XF] public void it_marks_each_entry_with_its_agent_CLI_and_has_a_toggle_for_each() => OnTheUIThread(() => _picker.TellsTheAgentClisApart).Must().BeTrue();

   [XF] public void it_has_a_toggle_for_Claude_Code_and_for_Codex() => OnTheUIThread(() => _picker.AgentClis).Must().Satisfy(it => it.SequenceEqual([AgentCli.ClaudeCode, AgentCli.Codex]));

   [XF] public void it_lists_the_sessions_of_both_at_first() => OnTheUIThread(() => _picker.AgentClisListed.ToHashSet()).Must().Satisfy(it => it.SetEquals([AgentCli.ClaudeCode, AgentCli.Codex]));

   [XF] public void each_is_called_by_its_name_else_its_prompt() =>
      OnTheUIThread(() => _picker.Listed.Select(it => it.Title).ToList()).Must().Satisfy(it => it.SequenceEqual(["Porting the parser", "Fixing the login", "Explain the build", "Tidy the build script"]));

   [XF] public void a_named_Codex_session_shows_its_first_prompt_under_its_name() => OnTheUIThread(() => _picker.Listed[0].Prompt).Must().Be("Port the parser to C#");

   public class and_leaves_out_Claude_Codes_sessions : When_Magnus_opens_the_session_history_picker_on_sessions_of_Claude_Code_and_Codex
   {
      public and_leaves_out_Claude_Codes_sessions() => Once(() => OnTheUIThread(() => _picker.LeaveOutTheSessionsOf(AgentCli.ClaudeCode)));

      [XF] public void it_lists_the_Codex_sessions_alone() => Listed.Must().Satisfy(it => it.SequenceEqual<SessionId>([NamedCodexs, UnnamedCodexs]));

      [XF] public void the_search_box_has_the_keyboard_focus() => OnTheUIThread(() => _picker.TheSearchBoxHasTheFocus).Must().BeTrue();

      public class and_then_Codexs_too : and_leaves_out_Claude_Codes_sessions
      {
         public and_then_Codexs_too() => Once(() => OnTheUIThread(() => _picker.LeaveOutTheSessionsOf(AgentCli.Codex)));

         [XF] public void it_lists_none() => Listed.Must().BeEmpty();

         public class and_lists_Claude_Codes_again : and_then_Codexs_too
         {
            public and_lists_Claude_Codes_again() => Once(() => OnTheUIThread(() => _picker.ListTheSessionsOf(AgentCli.ClaudeCode)));

            [XF] public void it_lists_the_Claude_Code_sessions_alone() => Listed.Must().Satisfy(it => it.SequenceEqual<SessionId>([TitledClaudeCodes, UntitledClaudeCodes]));
         }
      }
   }

   public class and_chooses_the_named_Codex_session : When_Magnus_opens_the_session_history_picker_on_sessions_of_Claude_Code_and_Codex
   {
      readonly SessionTab _tab;

      public and_chooses_the_named_Codex_session() => _tab = Once(() =>
      {
         Press(_window, Key.Enter, PhysicalKey.Enter, RawInputModifiers.None);
         WaitUntil(() => _window.Tabs.SingleOrDefault()?.SessionRunningInIt?.Session.Id == NamedCodexs, "the Codex session was resumed in a tab");
         var tab = OnTheUIThread(() => _window.Tabs.Single());
         Render(_window, "a-Codex-session-resumed-from-the-history-picker");
         return tab;
      });

      [XF] public void the_picker_closes() => OnTheUIThread(() => _picker.IsOpen).Must().BeFalse();

      [XF] public void it_resumes_it_with_Codex_in_a_new_tab_which_is_selected() => OnTheUIThread(() => _window.SelectedTab).Must().ReferenceEqual(_tab);

      [XF] public void the_hub_lists_Codex_running_it_in_the_tab() => OnTheUIThread(() => _tab.SessionRunningInIt).Must().Satisfy(it => it is RunningCodex);

      [XF] public void the_tab_is_titled_with_the_sessions_name() => OnTheUIThread(() => _tab.Title).Must().Be("Porting the parser");

      [XF] public void the_tab_offers_no_chat() => OnTheUIThread(() => _tab.OffersTheChat).Must().BeFalse();
   }
}
