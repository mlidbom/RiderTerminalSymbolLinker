using Avalonia.Controls;
using Avalonia.Input;
using Cloyd.Chat.Writing;
using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Writing;

/// <summary>
/// Magnus has Claude work for 5 seconds, presses Escape in the chat's input box meanwhile, and sends another message:
/// nothing interrupts Claude by accident (Magnus, 2026-11-21: "I do not want sending a message to kill the ongoing work
/// either. Absolutely not."). Claude Code queues the message, and takes it once it is done. Until the conversation has
/// it, it shows over the input box, dimmed, as Claude Code's own UI shows a queued message (Magnus, 2026-11-21: "it should
/// not appear to disappear until the agent picks it up").
/// </summary>
public class When_Magnus_sends_a_message_while_Claude_works : SpecificationWritingInAChat
{
   readonly SessionTab _tab;
   readonly bool _theStopButtonShowedWhileClaudeWorked;
   readonly IReadOnlyList<(string Text, string Stage)> _onTheirWayAsSent;
   readonly IReadOnlyList<(string Text, string Stage)> _onTheirWayOnceQueued;

   public When_Magnus_sends_a_message_while_Claude_works() => (_tab, _theStopButtonShowedWhileClaudeWorked, _onTheirWayAsSent, _onTheirWayOnceQueued) = Once(() =>
   {
      var (window, tab) = AChatOfANewSessionIn(Sandbox.Project("the-project"));
      TypeAndSend(window, "Be busy for 5 seconds");
      WaitUntil(() => tab.Chat.InputBox.ShowsTheStopButton, "the Stop button showed, as Claude worked");
      Press(window, Key.Escape, PhysicalKey.Escape, RawInputModifiers.None);
      TypeAndSend(window, "Meanwhile");
      var asSent = OnTheirWay(tab);
      WaitUntil(() => tab.Chat.InputBox.MessagesOnTheirWay is [{ IsQueued: true }], "Claude Code queued the message");
      var onceQueued = OnTheirWay(tab);
      Render(window, "a-message-queued-over-the-input-box");
      WaitUntil(() => RepliesIn(tab).Contains("Heard: Be busy for 5 seconds") && PromptsIn(tab).Contains("Meanwhile"), "Claude replied, and Claude Code took the message sent meanwhile");
      return (tab, true, asSent, onceQueued);
   });

   static IReadOnlyList<(string Text, string Stage)> OnTheirWay(SessionTab tab) =>
      OnTheUIThread(() => tab.Chat.InputBox.MessagesOnTheirWay.Select(it => (it.Message.AsShownInTheChat, it.Stage)).ToList());

   [XF] public void the_stop_button_showed_while_Claude_worked() => _theStopButtonShowedWhileClaudeWorked.Must().BeTrue();

   [XF] public void Claude_finished_its_turn() => RepliesIn(_tab).Must().Satisfy(it => it.Contains("Heard: Be busy for 5 seconds"));

   [XF] public void Claude_Code_took_the_message_once_Claude_was_done() => PromptsIn(_tab).Must().Satisfy(it => it.SequenceEqual(["Be busy for 5 seconds", "Meanwhile"]));

   [XF] public void the_stop_button_goes_once_Claude_is_done() => WaitUntil(() => !_tab.Chat.InputBox.ShowsTheStopButton, "the Stop button went");

   [XF] public void as_sent_the_message_shows_over_the_box_as_being_sent() =>
      _onTheirWayAsSent.Must().Satisfy(it => it.SequenceEqual([("Meanwhile", MessageOnItsWay.Sending)]));

   [XF] public void once_Claude_Code_queued_it_it_shows_over_the_box_as_queued() =>
      _onTheirWayOnceQueued.Must().Satisfy(it => it.SequenceEqual([("Meanwhile", MessageOnItsWay.Queued)]));

   [XF] public void once_the_conversation_has_it_it_shows_over_the_box_no_more() =>
      WaitUntil(() => _tab.Chat.InputBox.MessagesOnTheirWay.Count == 0, "the message on its way showed over the box no more");

   /// <summary>Magnus, 2026-11-21: "place queued messages above the turn information with the spinning icon, but below what the session is doing currently which is the reason the message is queued".</summary>
   [XF] public void it_shows_under_the_call_under_way_and_over_the_turns_line() =>
      OnTheUIThread(() => (Grid.GetRow(_tab.Chat.CallUnderWay), Grid.GetRow(_tab.Chat.MessagesOnTheirWay), Grid.GetRow(_tab.Chat.WorkingLine)))
        .Must().Satisfy(rows => rows.Item1 < rows.Item2 && rows.Item2 < rows.Item3);
}
