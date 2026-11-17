namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   A folder of the .NET Analysis&amp;Refactoring MCP's tools builds. A tools build is the part of the MCP that answers
///   requests; the part Rider loads as the plugin, its host, serves the newest one, and swaps to a newer one at its next
///   call, without a restart.
/// </summary>
/// <remarks>
///   Each build is a folder named by the time it was deployed, so the newest sorts last. It holds the tools assembly and
///   <c>host.sha256</c>, the hash of the host it was built against: a host serves only builds built against itself. The
///   MCP's <c>dev_docs/swappable-tools.md</c> has the design.
/// </remarks>
public sealed class McpToolsBuilds(string folder)
{
   const string IncompleteSuffix = ".incomplete";

   /// <summary>The folder that holds the builds.</summary>
   public string Folder { get; } = folder;

   /// <summary>
   ///   Makes this folder hold the same builds as <paramref name="source"/>, and returns whether that changed anything.
   /// </summary>
   /// <remarks>
   ///   Each build is copied under a name ending in <c>.incomplete</c> and renamed once complete, as the MCP deploys them,
   ///   so a running host never loads half a build. The host loads a build from its bytes, so no build is ever locked,
   ///   and one can be deleted while it's served.
   /// </remarks>
   public bool TakeTheBuildsOf(McpToolsBuilds source)
   {
      Directory.CreateDirectory(Folder);
      foreach(var leftover in Directory.GetDirectories(Folder, "*" + IncompleteSuffix)) Directory.Delete(leftover, recursive: true);

      var changed = false;
      foreach(var build in source.Builds().Except(Builds()).ToList())
      {
         var incomplete = Path.Combine(Folder, build + IncompleteSuffix);
         CopyFolder(Path.Combine(source.Folder, build), incomplete);
         Directory.Move(incomplete, Path.Combine(Folder, build));
         changed = true;
      }

      foreach(var build in Builds().Except(source.Builds()).ToList())
      {
         Directory.Delete(Path.Combine(Folder, build), recursive: true);
         changed = true;
      }

      return changed;

      static void CopyFolder(string from, string to)
      {
         Directory.CreateDirectory(to);
         foreach(var file in Directory.GetFiles(from)) File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
         foreach(var folder in Directory.GetDirectories(from)) CopyFolder(folder, Path.Combine(to, Path.GetFileName(folder)));
      }
   }

   /// <summary>
   ///   Removes every build, so that a host falls back to the builds its plugin ships with. Returns whether there were
   ///   any.
   /// </summary>
   public bool Clear()
   {
      if(!Builds().Any()) return false;
      Directory.Delete(Folder, recursive: true);
      return true;
   }

   /// <summary>
   ///   The name of the build that the host whose hash is <paramref name="hostHash"/> serves from this folder: the newest
   ///   built against it, or null if none is.
   /// </summary>
   public string? NewestBuiltAgainst(string hostHash) =>
      Builds().Order(StringComparer.Ordinal)
              .LastOrDefault(it => string.Equals(File.ReadAllText(Path.Combine(Folder, it, "host.sha256")).Trim(), hostHash, StringComparison.OrdinalIgnoreCase));

   IEnumerable<string> Builds() =>
      Directory.Exists(Folder)
         ? Directory.GetDirectories(Folder).Select(it => Path.GetFileName(it)).Where(it => !it.EndsWith(IncompleteSuffix, StringComparison.Ordinal))
         : [];
}
