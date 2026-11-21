using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Writing;

/// <summary>Magnus sends a slash command from a chat's input box: the tab shows its terminal, where Claude Code shows what the command does.</summary>
public class When_Magnus_sends_a_slash_command_from_a_chat : SpecificationWritingInAChat
{
   readonly SessionTab _tab;

   public When_Magnus_sends_a_slash_command_from_a_chat()
   {
      var (window, tab) = AChatOfANewSessionIn(Sandbox.Project("the-project"));
      _tab = tab;
      TypeAndSend(window, "/rename renamed from the chat");
   }

   [XF] public void the_tab_shows_its_terminal() => OnTheUIThread(() => _tab.ShowsTheChat).Must().BeFalse();
}
