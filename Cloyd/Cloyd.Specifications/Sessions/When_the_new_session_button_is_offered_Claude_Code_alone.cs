using Avalonia.Controls;
using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Sessions;

/// <summary>
/// The <see cref="NewSessionButton"/>, offered Claude Code alone, as a project window's is from a hub whose announcement
/// names no Codex folder.
/// </summary>
public class When_the_new_session_button_is_offered_Claude_Code_alone : SpecificationOpeningWindows
{
   readonly NewSessionButton _button;

   public When_the_new_session_button_is_offered_Claude_Code_alone()
   {
      _button = OnTheUIThread(() => new NewSessionButton());
      Open(() => new Window { Content = _button, Width = 600, Height = 300 });
      OnTheUIThread(() =>
      {
         _button.Flyout!.ShowAt(_button);
         _button.Offer([AgentCli.ClaudeCode]);
      });
   }

   [XF] public void Claude_Code_is_chosen() => OnTheUIThread(() => _button.Chosen).Must().ReferenceEqual(AgentCli.ClaudeCode);

   [XF] public void its_box_shows_no_choice_of_agent_CLI() => OnTheUIThread(() => _button.ShowsTheChoiceOfAgentCli).Must().BeFalse();
}
