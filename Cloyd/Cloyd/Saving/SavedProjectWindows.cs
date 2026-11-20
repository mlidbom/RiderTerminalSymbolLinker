using System.Text.Json;
using Cloyd.Projects;

namespace Cloyd.Saving;

/// <summary>
/// Where Cloyd saves its <see cref="ProjectWindow"/>s, a file for each project, so that the project's next window brings
/// its tabs back: after Cloyd restarts, and after the machine does.
/// </summary>
sealed class SavedProjectWindows(DirectoryInfo folder)
{
   static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

   readonly DirectoryInfo _folder = folder;

   /// <summary>What was saved last of <paramref name="project"/>'s window: <see cref="SavedProjectWindow.NothingSaved"/> when nothing was.</summary>
   public SavedProjectWindow Of(Project project) =>
      File.Exists(FileOf(project)) ? JsonSerializer.Deserialize<SavedProjectWindow>(File.ReadAllText(FileOf(project)), Json)! : SavedProjectWindow.NothingSaved;

   /// <summary>Replaces what is saved of <paramref name="project"/>'s window with <paramref name="window"/>, at once: a reader never finds half of it.</summary>
   public void Save(Project project, SavedProjectWindow window)
   {
      _folder.Create();
      var writing = $"{FileOf(project)}.writing";
      File.WriteAllText(writing, JsonSerializer.Serialize(window, Json));
      File.Move(writing, FileOf(project), overwrite: true);
   }

   /// <summary>
   /// The file of <paramref name="project"/>'s window, named as Claude Code names a project's folder of transcripts: each
   /// character of its folder that is neither a letter nor a digit made a <c>-</c>. In lower case, since Windows' paths
   /// ignore case, as a <see cref="Project"/>'s equality does.
   /// </summary>
   string FileOf(Project project) =>
      Path.Combine(_folder.FullName, $"{new string([.. project.Folder.ToLowerInvariant().Select(it => char.IsAsciiLetterOrDigit(it) ? it : '-')])}.json");
}
