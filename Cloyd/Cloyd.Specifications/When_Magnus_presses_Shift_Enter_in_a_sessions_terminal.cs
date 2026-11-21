using Avalonia.Input;
using Compze.Must;
using Compze.xUnitBDD;
using Cloyd.Sessions;
using Cloyd.Specifications.TheSandbox;

namespace Cloyd.Specifications;

/// <summary>
/// Shift+Enter in Claude Code's input box, as Magnus presses it in every editor to start a new line, and as he did in the
/// IDE's terminal: in a Windows console Claude Code ignores the Shift, and would send the message.
/// </summary>
public class When_Magnus_presses_Shift_Enter_in_a_sessions_terminal : SpecificationOpeningWindows
{
   readonly SessionTab _tab;

   public When_Magnus_presses_Shift_Enter_in_a_sessions_terminal()
   {
      var project = Sandbox.Project("the-project");
      var window = Open(project.Window);
      DoneOnTheUIThread(() => window.StartANewSession(new NewSession(AgentCli.ClaudeCode, Name: null)));
      _tab = OnTheUIThread(() => window.Tabs.Single());
      WaitUntil(() => _tab.SessionRunningInIt is not null && _tab.Terminal.TextShown.Contains("New session"), "the tab's terminal showed the stand-in's start");
      SwitchWithCtrlH(window, _tab);
      Type(window, "first");
      Press(window, Key.Enter, PhysicalKey.Enter, RawInputModifiers.Shift);
      Type(window, "second");
      WaitUntil(() => LinesOfTheInputBox.Contains("second"), "the input box showed what was typed after Shift+Enter");
      Render(window, "Shift-Enter-pressed-between-two-lines");
   }

   [XF] public void claude_codes_input_box_holds_what_was_typed_on_two_lines() => LinesOfTheInputBox.Must().Be("❯ first\n  second");

   string LinesOfTheInputBox => OnTheUIThread(() => InputBoxOfTheStandIn.LinesShownIn(_tab.Terminal));
}
