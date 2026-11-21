using ClaudeTooling.Hub.LocalApi.Contract.RunningSessions;

namespace Cloyd.Hub.RunningSessions;

/// <summary>What a <see cref="TurnUnderWay"/> does now: waiting for the model, thinking, writing, writing a tool call, or running tools.</summary>
/// <param name="Kind">
/// As the hub names it: <c>requesting</c> (Claude Code has sent the model a request, and no response streams yet),
/// <c>thinking</c>, <c>writing</c>, <c>writingAToolCall</c>, or <c>runningTools</c> (tools run, or wait for Magnus to allow
/// one). Kinds may be added.
/// </param>
/// <param name="ToolName">While <c>writingAToolCall</c>, the tool of the call the model writes, such as <c>Write</c>: null otherwise.</param>
sealed record ActivityOfATurn(string Kind, string? ToolName)
{
   /// <summary>The activity as the hub's answer <paramref name="activity"/> has it.</summary>
   public static ActivityOfATurn InThe(ActivityInTheAnswer activity) => new(activity.Kind, activity.ToolName);

   /// <summary>Whether tools run, or wait for Magnus to allow one: the conversation has each call, and each result as it comes.</summary>
   public bool IsRunningTools => Kind == "runningTools";

   /// <summary>
   /// What the turn does, as the chat's working line says it, such as "Thinking…" or "Writing the Write call…": a kind
   /// added after Cloyd was built is "Working…".
   /// </summary>
   public string AsShown => Kind switch
   {
      "requesting" => "Waiting for the model…",
      "thinking" => "Thinking…",
      "writing" => "Writing…",
      "writingAToolCall" => ToolName is {} tool ? $"Writing the {tool} call…" : "Writing a tool call…",
      "runningTools" => "Running tools…",
      _ => "Working…"
   };
}
