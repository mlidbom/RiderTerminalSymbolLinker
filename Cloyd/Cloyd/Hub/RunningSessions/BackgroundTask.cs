using ClaudeTooling.Hub.LocalApi.Contract.RunningSessions;

namespace Cloyd.Hub.RunningSessions;

/// <summary>
/// A task a running Claude Code runs in the background, as its <see cref="BackgroundWork"/> lists it: it goes on after
/// the turn that started it is over, and wakes the session once done. Such as a shell command Claude ran in the
/// background, a monitor, a subagent or a workflow.
/// </summary>
/// <param name="Kind">
/// What kind of task it is, as Claude Code labels it: <c>shell</c>, <c>subagent</c>, <c>monitor</c> or <c>workflow</c>.
/// Others may come, as Claude Code's own names for kinds it has no label for.
/// </param>
/// <param name="Description">What it does, in Claude's words, such as "Run ping in background".</param>
/// <param name="Command">A <c>shell</c> task's command line: null for the other kinds.</param>
/// <param name="AgentType">A <c>subagent</c>'s type, such as <c>Explore</c>: null for the other kinds.</param>
sealed record BackgroundTask(string Kind, string Description, string? Command, string? AgentType)
{
   /// <summary>The task as the hub's answer <paramref name="task"/> has it.</summary>
   public static BackgroundTask InThe(BackgroundTaskInTheAnswer task) => new(task.Kind, task.Description, task.Command, task.AgentType);

   /// <summary>
   /// Its kind and what it does, with a shell's command on a line under it, such as<br/>
   /// <c>shell: Run ping in background</c><br/>
   /// <c>   ping -n 40 127.0.0.1</c>
   /// </summary>
   public string AsListed => $"{Kind}{(AgentType is {} type ? $" {type}" : "")}: {Description}{(Command is {} command ? $"\n   {command}" : "")}";
}
