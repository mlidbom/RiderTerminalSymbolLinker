using ClaudeTooling.Hub.LocalApi.Contract.Conversations.Messages;

namespace Cloyd.Chat;

partial record ShownInTheChat
{
   /// <summary>A slash command Magnus sent, in a bubble at the right as his prompts are, the command in blue.</summary>
   /// <param name="Id">The message's id.</param>
   /// <param name="AsTyped">The command as typed, such as <c>/compact keep the fix</c>.</param>
   public sealed record SlashCommand(string Id, string AsTyped) : ShownInTheChat
   {
      /// <summary>
      /// Whether <paramref name="command"/> is the <c>/compact</c> that asked for <paramref name="shownBefore"/>, the
      /// compaction Claude Code writes before it: the app shows the command first, then the compaction's line.
      /// </summary>
      public static bool AskedFor(ShownInTheChat? shownBefore, MessageInTheAnswer.SlashCommand command) =>
         shownBefore is Compaction && (command.AsTyped == "/compact" || command.AsTyped.StartsWith("/compact ", StringComparison.Ordinal));

      /// <summary>The command, such as <c>/compact</c>.</summary>
      public string Command => AsTyped.Split(' ', 2)[0];

      /// <summary>What follows the command, such as <c> keep the fix</c>, with the space before it: empty when nothing does.</summary>
      public string Arguments => AsTyped[Command.Length..];
   }
}
