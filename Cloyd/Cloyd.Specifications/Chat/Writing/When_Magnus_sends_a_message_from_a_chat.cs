using Avalonia.Input;
using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Writing;

/// <summary>
/// Magnus writes a message of two lines in a chat's input box, Enter between them, and sends it with Ctrl+Enter: the hub
/// types it into Claude Code's own box, and sends it.
/// </summary>
public class When_Magnus_sends_a_message_from_a_chat : SpecificationWritingInAChat
{
   readonly SessionTab _tab;

   public When_Magnus_sends_a_message_from_a_chat() => _tab = Once(() =>
   {
      var (window, tab) = AChatOfANewSessionIn(Sandbox.Project("the-project"));
      Type(window, "first");
      Press(window, Key.Enter, PhysicalKey.Enter, RawInputModifiers.None);
      TypeAndSend(window, "second");
      WaitUntil(() => RepliesIn(tab).Count > 0, "Claude replied");
      Render(window, "a-message-sent-from-a-chat");
      return tab;
   });

   [XF] public void the_chat_shows_it_as_he_wrote_it_on_two_lines() => PromptsIn(_tab).Must().Satisfy(it => it.SequenceEqual(["first\nsecond"]));

   [XF] public void claude_code_got_it() => RepliesIn(_tab).Must().Satisfy(it => it.SequenceEqual(["Heard: first\nsecond"]));

   [XF] public void the_input_box_is_empty_again() => TextIn(_tab).Must().Be("");
}
