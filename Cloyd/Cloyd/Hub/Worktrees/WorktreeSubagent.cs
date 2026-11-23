namespace Cloyd.Hub.Worktrees;

/// <summary>A Codex subagent inspected with its parent during worktree removal.
/// A completed helper may still be loaded in Codex after its turn ends.</summary>
sealed record WorktreeSubagent(string SessionId, string ParentSessionId, string? ParentName, string? Name, string? AgentPath,
                              string Activity, bool BlocksRemoval, string? Reason)
{
   [System.Text.Json.Serialization.JsonIgnore] public string DisplayName =>
      !string.IsNullOrWhiteSpace(Name) ? Name : !string.IsNullOrWhiteSpace(AgentPath) ? AgentPath : $"Codex helper · {SessionId}";
}
