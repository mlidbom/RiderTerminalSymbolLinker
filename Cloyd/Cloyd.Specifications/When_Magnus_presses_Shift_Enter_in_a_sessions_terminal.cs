using Avalonia.Input;
using Compze.Must;
using Compze.xUnitBDD;
using Cloyd.Sessions;

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
      DoneOnTheUIThread(() => window.StartANewSession(name: null));
      _tab = OnTheUIThread(() => window.Tabs.Single());
      WaitUntil(() => _tab.SessionRunningInIt is not null && _tab.Terminal.TextShown.Contains("New session"), "the tab's terminal showed the stand-in's start");
      Focus(_tab.Terminal);
      Type(window, "first");
      Press(window, Key.Enter, PhysicalKey.Enter, RawInputModifiers.Shift);
      Type(window, "second");
      WaitUntil(() => LinesOfTheInputBox.Contains("second"), "the input box showed what was typed after Shift+Enter");
      Render(window, "Shift-Enter-pressed-between-two-lines");
   }

   [XF] public void claude_codes_input_box_holds_what_was_typed_on_two_lines() => LinesOfTheInputBox.Must().Be("❯ first\n  second");

   /// <summary>The lines of the stand-in's input box, as its terminal shows them: from the line with its prompt sign to the line before its closing rule, each trimmed at its end.</summary>
   string LinesOfTheInputBox => OnTheUIThread(() =>
   {
      var lines = _tab.Terminal.TextShown.Split('\n').Select(it => it.TrimEnd()).ToList();
      var first = lines.FindLastIndex(it => it.StartsWith('❯'));
      return first < 0 ? "" : string.Join("\n", lines.Skip(first).TakeWhile(it => !it.StartsWith('─')));
   });
}
