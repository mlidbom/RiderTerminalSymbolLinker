using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Writing;

/// <summary>
/// Magnus types into Claude Code's own box in the terminal, leaves it there, and sends a message from the chat: the hub
/// types nothing into a box that holds text, so that neither is mangled. The message comes back into the chat's box.
/// </summary>
public class When_Claude_Code_doesnt_get_a_message_from_the_chat : SpecificationWritingInAChat
{
   readonly SessionTab _tab;

   public When_Claude_Code_doesnt_get_a_message_from_the_chat() => _tab = Once(() =>
   {
      var (window, tab) = AChatOfANewSessionIn(Sandbox.Project("the-project"));
      SwitchWithCtrlH(window, tab);
      Type(window, "typed in the terminal");
      WaitUntil(() => tab.Terminal.TextShown.Contains("typed in the terminal"), "Claude Code's box showed what was typed in the terminal");
      SwitchWithCtrlH(window, tab);
      TypeAndSend(window, "from the chat");
      WaitUntil(() => tab.ClaudeCodeChat.InputBox.Notice is not null, "the chat's input box told why");
      Render(window, "a-message-claude-code-didnt-get");
      return tab;
   });

   [XF] public void the_message_is_back_in_the_box() => TextIn(_tab).Must().Be("from the chat");

   [XF] public void the_box_tells_why() => OnTheUIThread(() => _tab.ClaudeCodeChat.InputBox.Notice).Must().Satisfy(it => it!.Contains("input box holds text"));

   [XF] public void the_chat_shows_no_message() => PromptsIn(_tab).Must().BeEmpty();
}
