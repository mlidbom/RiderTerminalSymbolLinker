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

   /// <summary>Whether the folder holds the transcript of <paramref name="session"/>, in any project's folder: whether <c>claude --resume</c> can continue it.</summary>
   public bool HoldsTheTranscriptOf(ClaudeCodeSessionId session)
   {
      var projects = new DirectoryInfo(Path.Combine(_folder.FullName, "projects"));
      return projects.Exists && projects.EnumerateDirectories().Any(project => File.Exists(Path.Combine(project.FullName, $"{session}.jsonl")));
   }

   public override string ToString() => _folder.FullName;
}
