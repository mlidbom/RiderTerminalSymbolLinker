using CloydSpike.Hub;

namespace CloydSpike.SessionHosts;

/// <summary>
/// A Claude Code session for trying the spike on: a session host labelled as the spike's, running pwsh in the spike's
/// <c>scratch-session</c> folder, with the real Claude Code in it, on Haiku, named <see cref="Name"/>.
/// </summary>
static class ThrowawaySession
{
   public const string Name = "cloyd-spike";

   /// <summary>Starts one, and returns its host once Claude Code is typed into its shell: Claude Code then takes a few seconds to start.</summary>
   public static async Task<SessionHostOfTheHub> Start(TheHub hub)
   {
      var folder = Directory.CreateDirectory(Path.Combine(FolderOfTheSpike.FullName, "scratch-session"));
      var host = await hub.StartASessionHost("pwsh.exe -NoLogo", folder, new Dictionary<string, string>
      {
         ["TERM"] = "xterm-256color",
         ["COLORTERM"] = "truecolor",
         ["ANTHROPIC_MODEL"] = "claude-haiku-5-5"
      });
      await hub.StartClaudeCodeIn(host, Name);
      return host;
   }

   /// <summary>The folder of <c>CloydSpike.slnx</c>, above the running executable.</summary>
   static DirectoryInfo FolderOfTheSpike
   {
      get
      {
         for(var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
            if(File.Exists(Path.Combine(folder.FullName, "CloydSpike.slnx"))) return folder;
         throw new InvalidOperationException($"No CloydSpike.slnx above {AppContext.BaseDirectory}.");
      }
   }
}
