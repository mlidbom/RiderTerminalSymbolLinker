using ClaudeTooling.Hub.LocalApi.Contract.Conversations.Messages;
using ClaudeTooling.Hub.LocalApi.Contract.Conversations.Messages.Prompts;

namespace Cloyd.Chat;

partial record ShownInTheChat
{
   /// <summary>A prompt Magnus gave, in a bubble at the right, as plain text.</summary>
   /// <param name="Id">The message's <see cref="MessageInTheAnswer.Id"/>.</param>
   /// <param name="Text">What he typed and pasted, in order, and each image he pasted as its placeholder, such as <c>[Image #1]</c>.</param>
   public sealed record Prompt(string Id, string Text) : ShownInTheChat
   {
      public override string TextShown => Text;

      public static Prompt Of(MessageInTheAnswer.PromptOfMagnuss prompt)
      {
         var images = 0;
         return new(prompt.Id, string.Concat(prompt.Parts.Select(it => it switch
         {
            PartOfAPromptInTheAnswer.TypedText typed => typed.Text,
            PartOfAPromptInTheAnswer.PastedText pasted => pasted.Text,
            PartOfAPromptInTheAnswer.PastedImage => $"[Image #{++images}]",
            _ => ""
         })));
      }
   }
}
