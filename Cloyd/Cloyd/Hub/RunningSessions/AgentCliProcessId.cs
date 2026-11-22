using System.Diagnostics;

namespace Cloyd.Hub.RunningSessions;

/// <summary>The local agent process identified by the hub's running-session inventory.
/// Its last observation of a Codex TUI can briefly outlive the process after its host ends.</summary>
readonly record struct AgentCliProcessId(int Value)
{
   public bool IsAlive()
   {
      try
      {
         using var process = Process.GetProcessById(Value);
         return !process.HasExited;
      }
      catch(ArgumentException) { return false; } // GetProcessById reports an already exited process with this exception.
   }
}
