using Avalonia.Input;
using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Writing;

/// <summary>
/// Magnus has Claude work for 3 seconds, presses Escape in the chat's input box meanwhile, and sends another message:
/// nothing interrupts Claude by accident (Magnus, 2026-11-21: "I do not want sending a message to kill the ongoing work
/// either. Absolutely not."). Claude Code queues the message, and takes it once it is done.
/// </summary>
public class When_Magnus_sends_a_message_while_Claude_works : SpecificationWritingInAChat
{
   readonly SessionTab _tab;
   readonly bool _theStopButtonShowedWhileClaudeWorked;

   public When_Magnus_sends_a_message_while_Claude_works() => (_tab, _theStopButtonShowedWhileClaudeWorked) = Once(() =>
   {
      var (window, tab) = AChatOfANewSessionIn(Sandbox.Project("the-project"));
      TypeAndSend(window, "Be busy for 3 seconds");
      WaitUntil(() => tab.Chat.InputBox.ShowsTheStopButton, "the Stop button showed, as Claude worked");
      Press(window, Key.Escape, PhysicalKey.Escape, RawInputModifiers.None);
      TypeAndSend(window, "Meanwhile");
      WaitUntil(() => RepliesIn(tab).Contains("Heard: Be busy for 3 seconds") && PromptsIn(tab).Contains("Meanwhile"), "Claude replied, and Claude Code took the message sent meanwhile");
      return (tab, true);
   });

   [XF] public void the_stop_button_showed_while_Claude_worked() => _theStopButtonShowedWhileClaudeWorked.Must().BeTrue();

   [XF] public void Claude_finished_its_turn() => RepliesIn(_tab).Must().Satisfy(it => it.Contains("Heard: Be busy for 3 seconds"));

   [XF] public void Claude_Code_took_the_message_once_Claude_was_done() => PromptsIn(_tab).Must().Satisfy(it => it.SequenceEqual(["Be busy for 3 seconds", "Meanwhile"]));

   [XF] public void the_stop_button_goes_once_Claude_is_done() => WaitUntil(() => !_tab.Chat.InputBox.ShowsTheStopButton, "the Stop button went");
}
