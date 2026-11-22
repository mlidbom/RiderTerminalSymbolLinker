using System.Text.Json;
using Cloyd.Projects;

namespace Cloyd.ClaudeCode;

/// <summary>
/// The folder Claude Code keeps its state in: <c>~/.claude</c>, unless <c>CLAUDE_CONFIG_DIR</c> names another, as a
/// sandbox's. Cloyd reads the transcripts of the sessions in it, which <c>claude --resume</c> continues.
/// </summary>
/// <remarks>
/// Each session's transcript is <c>projects/&lt;a name made from the folder it started in&gt;/&lt;its id&gt;.jsonl</c>.
/// Archiving or deleting a session moves or removes it, and <c>claude --resume</c> then fails.
/// </remarks>
sealed class ClaudeCodeFolder(DirectoryInfo folder)
{
   readonly DirectoryInfo _folder = folder;

   /// <summary>Existing projects recorded in Claude Code's global configuration. Accepting the trust dialog
   /// for a folder covers its recorded subfolders, as in ClaudesSetupForCodex.</summary>
   public IReadOnlySet<Project> TrustedProjects()
   {
      var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
      var defaultFolder = Path.Combine(home, ".claude");
      var isDefault = string.Equals(_folder.FullName, defaultFolder, StringComparison.OrdinalIgnoreCase);
      var explicitlySelected = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") is { Length: > 0 } configured &&
                               string.Equals(Path.GetFullPath(configured), _folder.FullName, StringComparison.OrdinalIgnoreCase);
      var configDirectory = isDefault && !explicitlySelected ? home : _folder.FullName;
      var file = new FileWrittenWhole(Path.Combine(configDirectory, ".claude.json"));
      if(file.Text() is not {} text) return new HashSet<Project>();
      using var json = JsonDocument.Parse(text);
      if(!json.RootElement.TryGetProperty("projects", out var projects)) return new HashSet<Project>();
      var recorded = new HashSet<Project>();
      var accepted = new HashSet<Project>();
      foreach(var entry in projects.EnumerateObject())
      {
         if(!Path.IsPathFullyQualified(entry.Name)) continue;
         var project = new Project(entry.Name);
         recorded.Add(project);
         if(entry.Value.ValueKind == JsonValueKind.Object && entry.Value.TryGetProperty("hasTrustDialogAccepted", out var trust) && trust.ValueKind == JsonValueKind.True)
            accepted.Add(project);
      }
      return recorded.Where(it => Directory.Exists(it.Folder) && accepted.Any(root => root.Contains(it))).ToHashSet();
   }

   /// <summary>Whether the folder holds the transcript of <paramref name="session"/>, in any project's folder: whether <c>claude --resume</c> can continue it.</summary>
   public bool HoldsTheTranscriptOf(ClaudeCodeSessionId session)
   {
      var projects = new DirectoryInfo(Path.Combine(_folder.FullName, "projects"));
      return projects.Exists && projects.EnumerateDirectories().Any(project => File.Exists(Path.Combine(project.FullName, $"{session}.jsonl")));
   }

   public override string ToString() => _folder.FullName;
}
