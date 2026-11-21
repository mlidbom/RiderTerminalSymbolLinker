using Cloyd.Chat.ToolCalls;

namespace Cloyd.Chat.Status;

/// <summary>
/// The tool calls the conversation has of its last turn: those after the last prompt, of Magnus's or from elsewhere, or
/// slash command, and after the last turn's end. What the working line tells of the calls of the turn under way.
/// </summary>
/// <remarks>
/// A turn's end counts as well as a prompt: a turn woken by a prompt the chat doesn't show, as a background task's notice
/// that it ended, is under way after the turn before it ended.
/// </remarks>
/// <param name="Calls">The calls, in the order Claude made them.</param>
sealed record ToolCallsOfTheLastTurn(IReadOnlyList<ToolCallInTheChat> Calls)
{
   /// <summary>The calls of the last turn <paramref name="conversation"/> has.</summary>
   public static ToolCallsOfTheLastTurn In(IReadOnlyList<ShownInTheChat> conversation) =>
      new([.. conversation.Reverse()
                          .TakeWhile(it => it is not (ShownInTheChat.Prompt or ShownInTheChat.PromptFromElsewhere or ShownInTheChat.SlashCommand or ShownInTheChat.EndOfATurn))
                          .OfType<ShownInTheChat.RunOfToolCalls>()
                          .Reverse()
                          .SelectMany(it => it.Calls)]);

   /// <summary>The call under way: the last that has no result yet. Null while none is.</summary>
   public ToolCallInTheChat? UnderWay => Calls.LastOrDefault(it => it.Outcome == OutcomeOfAToolCall.Running);

   /// <summary>How many commands the turn has run, or runs.</summary>
   public int Commands => Calls.Count(it => it.Kind == KindOfToolCall.Running);
}
