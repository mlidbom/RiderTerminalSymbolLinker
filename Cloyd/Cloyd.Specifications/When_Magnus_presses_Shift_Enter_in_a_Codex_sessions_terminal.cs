using Avalonia.Input;
using Compze.Must;
using Compze.xUnitBDD;
using Cloyd.Sessions;

namespace Cloyd.Specifications;

/// <summary>Shift+Enter in a Codex session's terminal keeps both lines in the input box until Magnus sends them.</summary>
public class When_Magnus_presses_Shift_Enter_in_a_Codex_sessions_terminal : SpecificationOpeningWindows
{
   [XF(Skip = "The stand-in for Codex submits on Shift+Enter. It needs to insert a new line as Codex does.")]
   public void both_lines_stay_in_codexs_input_box()
   {
      var window = Open(Sandbox.Project("the-project").Window);
      DoneOnTheUIThread(() => window.StartANewSession(new NewSession(AgentCli.Codex, Name: null)));
      var terminal = OnTheUIThread(() => window.Tabs.Single().Terminal);
      WaitUntil(() => terminal.TextShown.Contains("New session"), "the stand-in for Codex showed its session");
      Type(window, "first");
      Press(window, Key.Enter, PhysicalKey.Enter, RawInputModifiers.Shift);
      Type(window, "second");
      WaitUntil(() => terminal.TextShown.Contains("second"), "the terminal showed the text typed after Shift+Enter");

      var lines = OnTheUIThread(() => terminal.TextShown).Split('\n').Select(it => it.Trim()).ToList();
      var inputBox = lines.Skip(lines.FindLastIndex(it => it.StartsWith('›'))).TakeWhile(it => it.Length > 0);
      string.Join("\n", inputBox).Must().Be("› first\nsecond");
   }
}
