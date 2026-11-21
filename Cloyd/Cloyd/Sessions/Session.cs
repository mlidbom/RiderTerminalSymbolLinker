using System.Text.Json.Serialization;
using Cloyd.Hub.Commands;

namespace Cloyd.Sessions;

/// <summary>
/// A session of an agent CLI, as a tab knows it: its id, its name, and the command that resumes it. Claude Code's is a
/// <see cref="ClaudeCode.ClaudeCodeSession"/>, Codex's a <see cref="Codex.CodexSession"/>. What happened in a session, as
/// its chat shows it, is each CLI's own.
/// </summary>
/// <param name="Name">The name the agent CLI shows for the session: null while it has none.</param>
[JsonConverter(typeof(SessionInJson))]
abstract partial record Session(string? Name)
{
   /// <summary>The agent CLI whose session it is.</summary>
   public abstract AgentCli AgentCli { get; }

   /// <summary>The id the agent CLI gave the session.</summary>
   public abstract SessionId Id { get; }

   /// <summary>The command that resumes the session in a session host's shell, which the hub types there.</summary>
   public abstract AgentCliCommand CommandThatResumesIt { get; }
}
