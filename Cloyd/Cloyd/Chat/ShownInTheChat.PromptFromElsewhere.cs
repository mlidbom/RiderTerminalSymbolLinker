using ClaudeTooling.Hub.LocalApi.Contract.Conversations.Messages;

namespace Cloyd.Chat;

partial record ShownInTheChat
{
   /// <summary>
   /// A prompt that isn't Magnus's, from another session, a scheduled wake-up or a program: a grey line, with the first line
   /// of what it said. The app wasn't seen showing one: Cloyd's own, until it is.
   /// </summary>
   /// <param name="Id">The message's id.</param>
   /// <param name="Line">The line, such as <c>From tooling-owner: Done, published.</c></param>
   public sealed record PromptFromElsewhere(string Id, string Line) : ShownInTheChat
   {
      /// <summary>
      /// Whether the chat shows <paramref name="prompt"/>: not a background task's notice, which the app doesn't show, Claude's
      /// answer to it following the turn before; nor Claude Code's own notices.
      /// </summary>
      public static bool IsShown(MessageInTheAnswer.PromptFromElsewhere prompt) => prompt.From is not ("backgroundTask" or "claudeCode");

      public static PromptFromElsewhere Of(MessageInTheAnswer.PromptFromElsewhere prompt) => new(prompt.Id, $"{FromWhom(prompt)}: {FirstLineOf(prompt.Text)}");

      static string FromWhom(MessageInTheAnswer.PromptFromElsewhere prompt) => prompt.From switch
      {
         "anotherSession" => $"From {prompt.SendingSessionName ?? "another session"}",
         "scheduledWakeUp" => "A scheduled wake-up",
         "program" => "From a program",
         var from => $"From {from}"
      };

      static string FirstLineOf(string text) => text.TrimStart().Split('\n', 2)[0].TrimEnd();
   }
}
