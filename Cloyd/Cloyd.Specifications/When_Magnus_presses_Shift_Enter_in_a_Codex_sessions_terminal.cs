using Avalonia.Input;
using Compze.Must;
using Compze.xUnitBDD;
using Cloyd.Sessions;
using Cloyd.Specifications.TheSandbox;

namespace Cloyd.Specifications;

/// <summary>
/// Shift+Enter in Codex's input box, as Magnus presses it in every editor to start a new line: Codex reads the Shift
/// itself, and starts the line, where Claude Code ignores it
/// (<see cref="When_Magnus_presses_Shift_Enter_in_a_Claude_Code_sessions_terminal"/>).
/// </summary>
public class When_Magnus_presses_Shift_Enter_in_a_Codex_sessions_terminal : SpecificationOpeningWindows
{
   readonly SessionTab _tab;

   public When_Magnus_presses_Shift_Enter_in_a_Codex_sessions_terminal()
   {
      var window = Open(Sandbox.Project("the-project").Window);
      DoneOnTheUIThread(() => window.StartANewSession(new NewSession(AgentCli.Codex, Name: null)));
      _tab = OnTheUIThread(() => window.Tabs.Single());
      WaitUntil(() => _tab.Terminal.TextShown.Contains("New session"), "the tab's terminal showed the stand-in for Codex's start");
      Type(window, "first");
      Press(window, Key.Enter, PhysicalKey.Enter, RawInputModifiers.Shift);
      Type(window, "second");
      WaitUntil(() => LinesOfTheInputBox.Contains("second"), "the input box showed what was typed after Shift+Enter");
      Render(window, "Shift-Enter-pressed-between-two-lines-in-Codex");
   }

   [XF(Skip = "The stand-in for Codex sends the prompt on Shift+Enter, where Codex starts a new line: gpt-collaborators is adding it (2026-11-22).")]
   public void codexs_input_box_holds_what_was_typed_on_two_lines() => LinesOfTheInputBox.Must().Be("› first\nsecond");

   string LinesOfTheInputBox => OnTheUIThread(() => InputBoxOfTheStandInForCodex.LinesShownIn(_tab.Terminal));
}
