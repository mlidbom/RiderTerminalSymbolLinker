using Cloyd.Hub.Commands;
using Cloyd.Hub.RunningSessions;

namespace Cloyd.Sessions;

/// <summary>
/// An agent CLI whose sessions Cloyd's tabs run, <see cref="ClaudeCode"/> or <see cref="Codex"/>: what differs between
/// their tabs, and the command that starts a new session of it.
/// </summary>
/// <remarks>
/// Only what a tab needs is here. What happened in a session, as a chat shows it, is each agent CLI's own, in types of
/// its own, such as <see cref="RunningClaudeCode"/>.
/// </remarks>
sealed class AgentCli
{
   public static AgentCli ClaudeCode { get; } = new("Claude Code", titleOfASessionWithNoName: "Claude", hasAChat: true, ClaudeCodeCommand.OfANewSession);

   /// <summary>Codex, whose tab shows its terminal only: Cloyd has no chat of a Codex session yet.</summary>
   public static AgentCli Codex { get; } = new("Codex", titleOfASessionWithNoName: "Codex", hasAChat: false, CodexCommand.OfANewSession);

   readonly Func<string?, AgentCliCommand> _commandOfANewSession;

   AgentCli(string name, string titleOfASessionWithNoName, bool hasAChat, Func<string?, AgentCliCommand> commandOfANewSession)
   {
      Name = name;
      TitleOfASessionWithNoName = titleOfASessionWithNoName;
      HasAChat = hasAChat;
      _commandOfANewSession = commandOfANewSession;
   }

   /// <summary>Its name, as Magnus knows it, such as "Claude Code".</summary>
   public string Name { get; }

   /// <summary>The title of a tab whose session of it has no name, such as "Claude".</summary>
   public string TitleOfASessionWithNoName { get; }

   /// <summary>Whether a tab of its session has a chat, beside the terminal its TUI draws in: else the terminal only.</summary>
   public bool HasAChat { get; }

   /// <summary>The command that starts a new session of it, named <paramref name="name"/>, or with no name when it is null.</summary>
   public AgentCliCommand CommandOfANewSession(string? name) => _commandOfANewSession(name);

   public override string ToString() => Name;
}
