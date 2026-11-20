using System.Globalization;
using ClaudeTooling.Hub.LocalApi.Contract.Conversations.Messages;

namespace Cloyd.Chat;

partial record ShownInTheChat
{
   /// <summary>
   /// Claude Code compacted the conversation: a grey line, <c>Compacted session · saved 81.5K tokens</c>. What came before
   /// stays above it.
   /// </summary>
   /// <param name="Id">The message's id.</param>
   /// <param name="TokensSaved">How many tokens the context went down by: null when Claude Code didn't record how many it held before.</param>
   public sealed record Compaction(string Id, long? TokensSaved) : ShownInTheChat
   {
      public static Compaction Of(MessageInTheAnswer.Compaction compaction) => new(compaction.Id, compaction.TokensBeforeIt - compaction.TokensAfterIt);

      /// <summary>The line, as the app writes it.</summary>
      public string Line => TokensSaved is {} saved ? $"Compacted session · saved {InThousands(saved)} tokens" : "Compacted session";

      static string InThousands(long tokens) => tokens >= 1000 ? string.Create(CultureInfo.InvariantCulture, $"{tokens / 1000.0:0.#}K") : tokens.ToString(CultureInfo.InvariantCulture);
   }
}
