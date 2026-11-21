using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

/// <summary>
/// Magnus opens the box of a project window's <see cref="NewSessionButton"/>, from the sandbox's hub, which serves Codex
/// sessions as the published hub does on a machine where npm installed Codex: no Codex session runs, nor has run.
/// </summary>
public class When_Magnus_opens_the_new_session_buttons_box : SpecificationOpeningWindows
{
   readonly NewSessionButton _button;

   public When_Magnus_opens_the_new_session_buttons_box()
   {
      var project = Sandbox.Project("the-project");
      var window = Open(project.Window);
      _button = OnTheUIThread(() => window.NewSessionButton);
      OnTheUIThread(() => _button.Flyout!.ShowAt(_button));
      WaitUntil(() => OnTheUIThread(() => _button.Offered.Count) > 1, "the box offered the agent CLIs the hub serves");
   }

   [XF] public void it_offers_Claude_Code_and_Codex() => OnTheUIThread(() => _button.Offered).Must().SequenceEqual([AgentCli.ClaudeCode, AgentCli.Codex]);

   [XF] public void Claude_Code_is_chosen() => OnTheUIThread(() => _button.Chosen).Must().ReferenceEqual(AgentCli.ClaudeCode);

   [XF] public void its_box_shows_the_choice_of_agent_CLI() => OnTheUIThread(() => _button.ShowsTheChoiceOfAgentCli).Must().BeTrue();
}
