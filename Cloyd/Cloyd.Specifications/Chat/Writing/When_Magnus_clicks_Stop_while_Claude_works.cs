using Cloyd.Chat.Writing;
using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Writing;

/// <summary>Magnus has Claude work for a minute, and clicks the Stop button beside the chat's input box: Claude stops.</summary>
public class When_Magnus_clicks_Stop_while_Claude_works : SpecificationWritingInAChat
{
   readonly SessionTab _tab;

   public When_Magnus_clicks_Stop_while_Claude_works() => _tab = Once(() =>
   {
      var (window, tab) = AChatOfANewSessionIn(Sandbox.Project("the-project"));
      TypeAndSend(window, "Be busy for 60 seconds");
      WaitUntil(() => tab.ClaudeCodeChat.InputBox.ShowsTheStopButton, "the Stop button showed, as Claude worked");
      Click(TheButtonWithTheTip(tab, InputBoxOfTheChat.TipOfTheStopButton));
      WaitUntil(() => !tab.ClaudeCodeChat.InputBox.ShowsTheStopButton, "the Stop button went, as Claude stopped");
      return tab;
   });

   [XF] public void Claude_gives_no_reply() => RepliesIn(_tab).Must().BeEmpty();

   [XF] public void the_box_tells_nothing() => OnTheUIThread(() => _tab.ClaudeCodeChat.InputBox.Notice).Must().BeNull();
}
