using RiderTerminalSymbolLinker.DevelopmentTools.Riders;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Plugin;

/// <summary>
///   One of the plugins this repository builds, by the name of its Gradle project, which is also its folder's name in a
///   plugins folder and in the <see cref="GradleSandbox"/>.
/// </summary>
/// <param name="Id">The plugin's ID, the <c>id</c> in its plugin.xml, which the IDE and the Marketplace know it by.</param>
/// <param name="RunsIn">The IDEs Magnus works in that <see cref="EverydayIdes.Deploy"/> puts it in.</param>
public sealed record OurPlugin(string Name, string Id, IReadOnlyList<EverydayIde> RunsIn)
{
   /// <summary>The .NET Terminal Symbol Linker, the root Gradle project: Rider only.</summary>
   public static readonly OurPlugin SymbolLinker = new("RiderTerminalSymbolLinker", "dev.magnus.riderterminalsymbollinker", [EverydayIde.Rider]);

   /// <summary>Claude Sessions, the <c>ClaudeSessions</c> Gradle project: the tool window of Claude Code sessions, for any JetBrains IDE.</summary>
   public static readonly OurPlugin ClaudeSessions = new("ClaudeSessions", "dev.magnus.claudesessions", [EverydayIde.Rider, EverydayIde.IntelliJIdea, EverydayIde.PyCharm]);

   public static IReadOnlyList<OurPlugin> All { get; } = [SymbolLinker, ClaudeSessions];

   /// <summary>The plugin named <paramref name="name"/>, in any case.</summary>
   public static OurPlugin Named(string name) =>
      All.SingleOrDefault(it => string.Equals(it.Name, name, StringComparison.OrdinalIgnoreCase))
   ?? throw new ArgumentException($"No plugin of ours is named '{name}': {string.Join(", ", All)} are.");

   /// <summary>
   ///   Makes the build of this plugin in <paramref name="pluginsFolder"/> look changed to the IDE that loads it from there,
   ///   by giving its jars the time of now: an IDE that reloads changed plugins then reloads it, as it does a new build.
   /// </summary>
   public void MakeItLookChangedIn(string pluginsFolder)
   {
      foreach(var jar in Directory.GetFiles(Path.Combine(pluginsFolder, Name, "lib"), "*.jar")) File.SetLastWriteTimeUtc(jar, DateTime.UtcNow);
   }

   public override string ToString() => Name;
}
