using System.Text.Json.Serialization;
using Cloyd.Sessions;

namespace Cloyd.LocalApi.Contract;

/// <summary>A named tab requested by a coordinator. The request ID identifies a single launch across retries.
/// The fingerprint includes the brief, which Peers delivers once it has the session's own address.</summary>
sealed record SessionToStartForAPeer([property: JsonPropertyName("requestId")] Guid RequestId,
                                     [property: JsonPropertyName("fingerprint")] string Fingerprint,
                                     [property: JsonPropertyName("project")] string Project,
                                     [property: JsonPropertyName("worktree")] string Worktree,
                                     [property: JsonPropertyName("agentCli")] string AgentCli,
                                     [property: JsonPropertyName("name")] string Name)
{
   [JsonIgnore] public NewSession Session => new(AgentCli == "codex" ? Sessions.AgentCli.Codex : Sessions.AgentCli.ClaudeCode, Name);

   [JsonIgnore] public bool IsValid => RequestId != Guid.Empty && Fingerprint is { Length: 64 } && Fingerprint.All(char.IsAsciiHexDigit)
                       && AgentCli is "codex" or "claude" && !string.IsNullOrWhiteSpace(Name) && Name.Trim() == Name && !Name.Any(char.IsControl)
                       && ExistingAbsoluteFolder(Project) && ExistingAbsoluteFolder(Worktree);

   static bool ExistingAbsoluteFolder(string? folder) =>
      !string.IsNullOrWhiteSpace(folder) && Path.IsPathFullyQualified(folder) && Directory.Exists(folder);

   public bool Matches(SessionToStartForAPeer other) =>
      RequestId == other.RequestId && Fingerprint == other.Fingerprint && Name == other.Name && AgentCli == other.AgentCli
      && string.Equals(Project, other.Project, StringComparison.OrdinalIgnoreCase)
      && string.Equals(Worktree, other.Worktree, StringComparison.OrdinalIgnoreCase);
}
