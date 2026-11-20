namespace Cloyd.Chat.ToolCalls;

/// <summary>How a tool call has ended, as its row tells: in the past tense once done, else in the present tense, with <c>Failed</c> or <c>Stopped</c> after it in red.</summary>
enum OutcomeOfAToolCall
{
   /// <summary>No result yet: its tool runs, or waits for Magnus to allow it.</summary>
   Running,

   /// <summary>Its tool ran, and didn't fail.</summary>
   Done,

   /// <summary>Its tool failed, as a file to read that doesn't exist.</summary>
   Failed,

   /// <summary>Magnus didn't let it run: he denied it, or interrupted Claude while it waited.</summary>
   Stopped
}
