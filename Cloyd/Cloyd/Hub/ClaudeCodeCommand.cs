using System.Text.Json.Nodes;
using Cloyd.ClaudeCode;

namespace Cloyd.Hub;

/// <summary>
/// The Claude Code that the hub types into a session host's shell, quoted as that shell reads it: <c>claude</c>, or
/// <c>claude --name &lt;name&gt;</c> for a new session, or <c>claude --resume &lt;id&gt;</c>.
/// </summary>
sealed class ClaudeCodeCommand
{
   /// <summary>The command, as a human would type it, before the shell's quoting: for what Cloyd tells.</summary>
   readonly string _asTyped;

   ClaudeCodeCommand(JsonObject body, string asTyped)
   {
      Body = body;
      _asTyped = asTyped;
   }

   /// <summary>What the hub's <c>POST /shells/{processId}/start-claude-code</c> is asked with.</summary>
   public JsonObject Body { get; }

   /// <summary>A new session: named <paramref name="name"/>, or with no name when it is null.</summary>
   public static ClaudeCodeCommand OfANewSession(string? name) =>
      name is null ? new([], "claude") : new(new JsonObject { ["name"] = name }, $"claude --name {name}");

   /// <summary>The session <paramref name="session"/>, continued from its transcript.</summary>
   public static ClaudeCodeCommand Resuming(ClaudeSession session) =>
      new(new JsonObject { ["sessionToResume"] = session.Id.Value }, $"claude --resume {session.Id}");

   public override string ToString() => _asTyped;
}
