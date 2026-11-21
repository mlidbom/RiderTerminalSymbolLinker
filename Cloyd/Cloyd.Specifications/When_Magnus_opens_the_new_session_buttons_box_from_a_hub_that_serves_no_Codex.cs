using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

/// <summary>
/// Magnus opens the box of a project window's <see cref="NewSessionButton"/>, from a hub that serves no Codex sessions, as
/// a hub of protocol 1 doesn't: the box offers Claude Code only, with no choice to make.
/// </summary>
public class When_Magnus_opens_the_new_session_buttons_box_from_a_hub_that_serves_no_Codex : SpecificationOpeningWindows
{
   readonly NewSessionButton _button;

   public When_Magnus_opens_the_new_session_buttons_box_from_a_hub_that_serves_no_Codex()
   {
      var project = Sandbox.Project("the-project");
      var window = Open(project.Window);
      _button = OnTheUIThread(() => window.NewSessionButton);
      OnTheUIThread(() => _button.Flyout!.ShowAt(_button));
   }

   [XF] public void it_offers_Claude_Code_only() => OnTheUIThread(() => _button.Offered).Must().SequenceEqual([AgentCli.ClaudeCode]);

   [XF] public void its_box_shows_no_choice_of_agent_CLI() => OnTheUIThread(() => _button.ShowsTheChoiceOfAgentCli).Must().BeFalse();
}
