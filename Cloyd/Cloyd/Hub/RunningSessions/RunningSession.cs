using ClaudeTooling.Hub.LocalApi.Contract;
using ClaudeTooling.Hub.LocalApi.Contract.RunningSessions;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Sessions;
using Cloyd.Projects;

namespace Cloyd.Hub.RunningSessions;

/// <summary>
/// A session that runs, of any agent CLI, as the hub's <c>GET /running-sessions</c> lists it: what a tab needs to know of
/// it, whichever agent CLI runs it. Claude Code's is a <see cref="RunningClaudeCode"/>, with what only Claude Code
/// tells, which its chat shows; Codex's a <see cref="RunningCodex"/>.
/// </summary>
/// <param name="SessionHost">The session host whose console the agent CLI runs in: null when it runs in none, as in a terminal of its own.</param>
abstract record RunningSession(SessionHostId? SessionHost)
{
   /// <summary>The working folder the hub reports for the agent. Its host's shell can have started in a different checkout.</summary>
   public Project? WorkingFolder { get; init; }
   AgentCliProcessId? AgentProcess { get; init; }

   /// <summary>Restoring a missing tab must neither duplicate a live agent nor discard a tab
   /// because its recently exited process is still in the hub's last observation.</summary>
   public bool IsLiveAgentOf(SessionId id) => Session.Id == id && (AgentProcess?.IsAlive() ?? true);

   /// <summary>The session that runs: its id and its name.</summary>
   public abstract Session Session { get; }

   /// <summary>Whether the agent CLI works at a turn, which the tab's header shows.</summary>
   public abstract bool IsBusy { get; }

   /// <summary>
   /// The running sessions <paramref name="answer"/> lists. One of an agent CLI Cloyd doesn't know is left out, as the
   /// hub's contract has a reader do.
   /// </summary>
   public static IReadOnlyList<RunningSession> EachIn(RunningSessionsAnswer answer) => [.. answer.RunningSessions.Select(InThe).OfType<RunningSession>()];

   /// <summary>The session as the hub's answer <paramref name="session"/> lists it: null when it's of an agent CLI Cloyd doesn't know.</summary>
   static RunningSession? InThe(RunningSessionInTheAnswer session)
   {
      RunningSession? agent = session.AgentCli switch
      {
         AgentCliNames.ClaudeCode => RunningClaudeCode.InThe(session),
         AgentCliNames.Codex => RunningCodex.InThe(session),
         _ => null
      };
      return agent is null ? null : agent with { AgentProcess = new AgentCliProcessId(session.ProcessId) };
   }

   protected static Project? WorkingFolderIn(RunningSessionInTheAnswer session) =>
      session.Directory is { Length: > 0 } folder && Path.IsPathFullyQualified(folder) ? new Project(folder) : null;
}
