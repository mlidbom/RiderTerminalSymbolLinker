using ClaudeTooling.Hub.LocalApi.Contract.Conversations;
using ClaudeTooling.Hub.LocalApi.Contract.Conversations.Messages;

namespace Cloyd.Hub;

/// <summary>
/// A session's conversation as one reading of the hub's stream of it has sent it so far: its messages, with each rewind
/// taken back, and the results of their tool calls. Read by the one thread that reads the stream.
/// </summary>
sealed class ConversationAsStreamed
{
   readonly List<MessageInTheAnswer> _messages = [];
   readonly Dictionary<string, ToolResultInTheAnswer> _toolResults = [];

   /// <summary>Adds <paramref name="message"/> at the end of the conversation.</summary>
   public void Add(MessageInTheAnswer message) => _messages.Add(message);

   /// <summary>Adds <paramref name="result"/>, the result of a tool call the stream sent.</summary>
   public void Add(ToolResultInTheAnswer result) => _toolResults[result.ToolUseId] = result;

   /// <summary>Takes back the messages after <paramref name="rewind"/>'s last message kept: all of them, when it kept none.</summary>
   public void Take(RewindInTheStream rewind)
   {
      var kept = rewind.LastMessageKept is {} last ? _messages.FindIndex(it => it.Id == last) + 1 : 0;
      _messages.RemoveRange(kept, _messages.Count - kept);
   }

   /// <summary>The conversation as sent so far, as the hub answers it whole: its own copy, which the stream's reading leaves as it is.</summary>
   public ConversationAnswer AsSentSoFar => new([.. _messages], [.. _toolResults.Values]);
}
