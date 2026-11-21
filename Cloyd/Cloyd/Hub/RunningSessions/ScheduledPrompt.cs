using ClaudeTooling.Hub.LocalApi.Contract.RunningSessions;

namespace Cloyd.Hub.RunningSessions;

/// <summary>
/// A prompt Claude Code will give a running session on a schedule, waking it then, as its <see cref="BackgroundWork"/>
/// lists it: what Claude's <c>CronCreate</c> and <c>ScheduleWakeup</c> tools and the <c>/loop</c> command schedule, for as
/// long as the session runs.
/// </summary>
/// <param name="Schedule">When it is given, as a cron expression, such as <c>37 14 21 11 *</c>.</param>
/// <param name="Recurring">Whether it is given at every time the schedule matches: false for a wake-up given once.</param>
/// <param name="Prompt">The prompt it gives.</param>
sealed record ScheduledPrompt(string Schedule, bool Recurring, string Prompt)
{
   /// <summary>What a scheduled prompt is called where background work is counted and listed, as the tasks' kinds are.</summary>
   public const string Kind = "scheduled prompt";

   /// <summary>The prompt as the hub's answer <paramref name="prompt"/> has it.</summary>
   public static ScheduledPrompt InThe(ScheduledPromptInTheAnswer prompt) => new(prompt.Schedule, prompt.Recurring, prompt.Prompt);

   /// <summary>When it is given and what it says, such as <c>scheduled prompt, once at 37 14 21 11 *: Check the CI run</c>.</summary>
   public string AsListed => $"{Kind}, {(Recurring ? "recurring" : "once")} at {Schedule}: {Prompt}";
}
