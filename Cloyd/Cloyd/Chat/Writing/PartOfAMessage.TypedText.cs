using ClaudeTooling.Hub.LocalApi.Contract.TypingIntoRunningSessions;

namespace Cloyd.Chat.Writing;

partial record PartOfAMessage
{
   /// <summary>Text Magnus typed: the hub types it as keystrokes, each line break as Ctrl+J, a new line in Claude Code's box.</summary>
   public sealed record TypedText(string Text) : PartOfAMessage
   {
      public override PartInTheRequest InTheRequest() => new(PartInTheRequest.TheKindOfTypedText, Text, File: null);
   }
}
