using ClaudeTooling.Hub.LocalApi.Contract.TypingIntoRunningSessions;

namespace Cloyd.Chat.Writing;

partial record PartOfAMessage
{
   /// <summary>
   /// Text Magnus pasted: the hub pastes it into Claude Code's box as a bracketed paste of its own, which Claude Code shows
   /// collapsed when it is long, and gives the model as pasted, inside <c>&lt;pasted_content&gt;</c> tags.
   /// </summary>
   public sealed record PastedText(string Text) : PartOfAMessage
   {
      public override PartInTheRequest InTheRequest() => new(PartInTheRequest.TheKindOfPastedText, Text, File: null);
   }
}
