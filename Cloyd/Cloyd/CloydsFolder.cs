using Cloyd.Pasting;
using Cloyd.Diagnostics;
using Cloyd.Saving;

namespace Cloyd;

/// <summary>
/// The folder where Cloyd keeps what it keeps on the machine: the <see cref="SavedProjectWindows"/>, the
/// <see cref="PastedImages"/>, and the announcement
/// of the Cloyd application's local API. <c>%LOCALAPPDATA%\Cloyd</c>, unless <see cref="VariableNamingAnother"/> names
/// another, as for a Cloyd the specifications run apart from Magnus's.
/// </summary>
sealed class CloydsFolder(DirectoryInfo folder)
{
   /// <summary>The environment variable that names another folder than <c>%LOCALAPPDATA%\Cloyd</c>.</summary>
   public const string VariableNamingAnother = "CLOYD_FOLDER";

   /// <summary>The folder itself.</summary>
   public DirectoryInfo Folder { get; } = folder;

   /// <summary>The folder Cloyd's environment names: <see cref="VariableNamingAnother"/>'s, else <c>%LOCALAPPDATA%\Cloyd</c>.</summary>
   public static CloydsFolder OfCloydsEnvironment() =>
      new(new DirectoryInfo(Environment.GetEnvironmentVariable(VariableNamingAnother) is {Length: > 0} another
                               ? another
                               : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cloyd")));

   /// <summary>Where Cloyd's project windows save themselves, a file for each project.</summary>
   public SavedProjectWindows SavedProjectWindows { get; } = new(new DirectoryInfo(Path.Combine(folder.FullName, "project-windows")));

   public SavedHostedSessions SavedHostedSessions { get; } = new(new DirectoryInfo(Path.Combine(folder.FullName, "hosted-sessions")));
   public FileWrittenWhole ProjectsDashboard => new(Path.Combine(Folder.FullName, "projects-dashboard.json"));
   public CrashReports CrashReports => new(new DirectoryInfo(Path.Combine(Folder.FullName, "crashes")));

   /// <summary>Where the images Magnus pastes into the sessions are saved, to be handed to Claude Code.</summary>
   public PastedImages PastedImages => new(new DirectoryInfo(Path.Combine(Folder.FullName, "pasted-images")));

   public override string ToString() => Folder.FullName;
}
