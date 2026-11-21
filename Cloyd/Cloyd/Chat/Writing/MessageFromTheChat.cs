using ClaudeTooling.Hub.LocalApi.Contract.TypingIntoRunningSessions;

namespace Cloyd.Chat.Writing;

/// <summary>
/// A message Magnus sent from the chat's input box: its <paramref name="Parts"/>, in the order they came into the box,
/// which the hub types into Claude Code's own input box, as he would, and sends.
/// </summary>
sealed record MessageFromTheChat(IReadOnlyList<PartOfAMessage> Parts)
{
   /// <summary>The message as the hub's <c>POST /running-sessions/{processId}/send</c> takes it.</summary>
   public MessageInTheRequest InTheRequest() => new([.. Parts.Select(it => it.InTheRequest())]);

   /// <summary>
   /// Whether it is a slash command, such as <c>/compact</c>: typed, with a <c>/</c> first. What it does shows in Claude
   /// Code's own UI, in the terminal.
   /// </summary>
   public bool IsASlashCommand => Parts is [PartOfAMessage.TypedText { Text: var first }, ..] && first.StartsWith('/');

   /// <summary>
   /// The message as the chat shows a prompt of Magnus's, <see cref="ShownInTheChat.Prompt.Text"/>: what he typed and
   /// pasted, in order, and each image as its placeholder, numbered from 1 within the message, such as <c>[Image #1]</c>.
   /// </summary>
   public string AsShownInTheChat
   {
      get
      {
         var images = 0;
         return string.Concat(Parts.Select(it => it switch
         {
            PartOfAMessage.TypedText typed => typed.Text,
            PartOfAMessage.PastedText pasted => pasted.Text,
            PartOfAMessage.PastedImage => $"[Image #{++images}]",
            _ => throw new ArgumentOutOfRangeException(nameof(Parts), it, "A part the chat doesn't know how to show.")
         }));
      }
   }
}
