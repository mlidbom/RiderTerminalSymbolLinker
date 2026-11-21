namespace Cloyd.Hub.Commands;

/// <summary>
/// An agent CLI's command line, which the hub types into a session host's shell: one that starts a new session, or one
/// that resumes a session. Claude Code's is a <see cref="ClaudeCodeCommand"/>.
/// </summary>
abstract class AgentCliCommand
{
   /// <summary>
   /// Has <paramref name="hub"/> type the command into the shell of <paramref name="host"/>, then Enter, once the shell
   /// has settled at its prompt: a <see cref="RefusedByTheHub"/> when it doesn't, as when a program runs in the shell.
   /// </summary>
   public abstract Task HaveTheHubTypeItInto(SessionHostOfTheHub host, TheHub hub, CancellationToken cancellation);

   /// <summary>The command, as a human would type it, before the shell's quoting: for what Cloyd tells.</summary>
   public abstract override string ToString();
}
