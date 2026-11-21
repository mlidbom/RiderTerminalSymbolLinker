using Avalonia.Controls;
using Avalonia.Input;
using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Sessions;

/// <summary>
/// The <see cref="NewSessionButton"/>, offering Claude Code and Codex, as a project window's does from a hub that serves
/// Codex sessions: Magnus opens its box, chooses Codex, types the session's name and presses Enter.
/// </summary>
public class When_Magnus_chooses_Codex_in_the_new_session_buttons_box : SpecificationOpeningWindows
{
   const string NameGiven = "Porting the parser";

   readonly NewSessionButton _button;
   readonly AgentCli _chosenAtFirst;
   readonly List<NewSession> _asked = [];

   public When_Magnus_chooses_Codex_in_the_new_session_buttons_box()
   {
      _button = OnTheUIThread(() => new NewSessionButton());
      var window = Open(() => new Window { Content = _button, Width = 600, Height = 300 });
      OnTheUIThread(() =>
      {
         _button.NewSessionAsked += _asked.Add;
         _button.Flyout!.ShowAt(_button);
         _button.Offer([AgentCli.ClaudeCode, AgentCli.Codex]);
      });
      _chosenAtFirst = OnTheUIThread(() => _button.Chosen);
      OnTheUIThread(() => _button.Choose(AgentCli.Codex));
      Type(window, NameGiven);
      Press(window, Key.Enter, PhysicalKey.Enter, RawInputModifiers.None);
   }

   [XF] public void its_box_shows_the_choice_of_agent_CLI() => OnTheUIThread(() => _button.ShowsTheChoiceOfAgentCli).Must().BeTrue();

   [XF] public void Claude_Code_was_chosen_at_first() => _chosenAtFirst.Must().ReferenceEqual(AgentCli.ClaudeCode);

   [XF] public void it_asks_for_a_new_Codex_session_with_the_name_typed() => _asked.Must().SequenceEqual([new NewSession(AgentCli.Codex, NameGiven)]);
}
