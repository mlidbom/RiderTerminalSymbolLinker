using ClaudeTooling.Hub.LocalApi.Contract.RunningSessions;

namespace Cloyd.Hub.RunningSessions;

/// <summary>
/// What a running Claude Code runs in the background, as the hub tells it with each running session: the tasks that go
/// on after the turn that started them, such as a shell command Claude ran in the background, which Claude Code's TUI
/// counts as "1 shell" beside its input box; and the prompts scheduled to wake the session, as <c>/loop</c> and Claude's
/// <c>ScheduleWakeup</c> schedule them. Magnus, 2026-11-21: "I have no way in the new UI to see that a session in waiting
/// on something".
/// </summary>
/// <remarks>
/// As of the end of the session's last turn, when Claude Code tells it to a <c>Stop</c> hook of the hub's: a task started
/// in a turn shows once the turn ends, and a task that ends wakes a turn, whose end tells what is left. So while a turn is
/// under way, a task listed may have ended since.
/// </remarks>
/// <param name="Tasks">The tasks that run in the background, or wait to: empty when none does.</param>
/// <param name="ScheduledPrompts">The prompts scheduled to wake the session: empty when none is.</param>
sealed record BackgroundWork(IReadOnlyList<BackgroundTask> Tasks, IReadOnlyList<ScheduledPrompt> ScheduledPrompts)
{
   /// <summary>
   /// What the chat tells while the hub knows only that some background work runs, by the session's status, and not what,
   /// as until a turn of the session has ended since the hub started: see <see cref="StatusOfAClaudeCode.IsIdleWithBackgroundWork"/>.
   /// </summary>
   public const string SomeRuns = "A background shell or monitor runs";

   /// <summary>The background work as the hub's answer <paramref name="work"/> has it.</summary>
   public static BackgroundWork InThe(BackgroundWorkInTheAnswer work) =>
      new([.. work.Tasks.Select(BackgroundTask.InThe)], [.. work.ScheduledPrompts.Select(ScheduledPrompt.InThe)]);

   /// <summary>Whether any task runs in the background, or any prompt is scheduled.</summary>
   public bool IsAny => Tasks.Count > 0 || ScheduledPrompts.Count > 0;

   /// <summary>
   /// It counted by kind, as Claude Code's TUI counts it beside its input box, such as "1 shell" or
   /// "2 shells · 1 monitor · 1 scheduled prompt": the tasks' kinds in the order the first of each is listed, then the
   /// scheduled prompts.
   /// </summary>
   public string Counted =>
      string.Join(" · ", [.. TasksByKind, .. ScheduledPrompts.Count > 0 ? [CountOf(ScheduledPrompts.Count, ScheduledPrompt.Kind)] : Array.Empty<string>()]);

   /// <summary>
   /// The tasks counted by kind, as the TUI counts them in the line after a turn that leaves them running, such as
   /// "1 shell, 1 monitor": null while none runs. A scheduled prompt doesn't run until it wakes the session.
   /// </summary>
   public string? TasksCounted => Tasks.Count > 0 ? string.Join(", ", TasksByKind) : null;

   /// <summary>The tasks counted by kind, such as "2 shells": the kinds in the order the first of each is listed.</summary>
   IEnumerable<string> TasksByKind => Tasks.GroupBy(it => it.Kind).Select(it => CountOf(it.Count(), it.Key));

   /// <summary>Each task and scheduled prompt with what it does, a line or two each, as the TUI's list of them shows it: see <see cref="BackgroundTask.AsListed"/>.</summary>
   public string Listed => string.Join("\n", [.. Tasks.Select(it => it.AsListed), .. ScheduledPrompts.Select(it => it.AsListed)]);

   static string CountOf(int count, string kind) => count == 1 ? $"1 {kind}" : $"{count} {kind}s";
}
