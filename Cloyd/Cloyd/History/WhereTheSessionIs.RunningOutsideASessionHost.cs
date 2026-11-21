using Cloyd.ClaudeCode;
using Cloyd.Projects;
using Cloyd.Sessions;

namespace Cloyd.History;

abstract partial class WhereTheSessionIs
{
   /// <summary>
   /// It runs as Claude Code's <paramref name="process"/>, in no session host, as in a terminal of its own: no tab can show
   /// it, so it can't be chosen.
   /// </summary>
   public sealed class RunningOutsideASessionHost(ClaudeCodeProcessId process) : WhereTheSessionIs
   {
      /// <summary>The process the session's Claude Code runs as.</summary>
      public ClaudeCodeProcessId Process { get; } = process;

      public override bool CanBeChosen => false;

      public override bool IsRunning => true;

      public override string AsToldIn(ProjectWindow pickedIn) => "running outside Cloyd";

      public override Task Choose(Session session, ProjectWindow pickedIn) =>
         throw new InvalidOperationException($"A session that runs in no session host can't be chosen: {session.Id} runs as process {Process}.");
   }
}
