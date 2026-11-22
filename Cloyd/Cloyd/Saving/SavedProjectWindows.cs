using System.Text.Json;
using ClaudeTooling.SessionHost.Protocol;
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
      FileOf(project).Text() is {} json ? JsonSerializer.Deserialize<SavedProjectWindow>(json, Json)! : SavedProjectWindow.NothingSaved;

   /// <summary>Replaces what is saved of <paramref name="project"/>'s window with <paramref name="window"/>, at once: a reader never finds half of it.</summary>
   public void Save(Project project, SavedProjectWindow window) => FileOf(project).Write(JsonSerializer.Serialize(window, Json));

   /// <summary>Ending a host in Projects Dashboard also removes its tab from closed project windows.
   /// A saved tab left there would resume the session in a new host when its project next opens.</summary>
   public void Forget(SessionHostId ended)
   {
      if(!_folder.Exists) return;
      foreach(var entry in _folder.EnumerateFiles("*.json"))
      {
         var file = new FileWrittenWhole(entry.FullName);
         if(file.Text() is not {} json || JsonSerializer.Deserialize<SavedProjectWindow>(json, Json) is not {} saved) continue;
         if(!saved.Tabs.Any(it => it.SessionHost == ended)) continue;
         var tabs = saved.Tabs.Where(it => it.SessionHost != ended).ToArray();
         var updated = saved with { Tabs = tabs, SelectedTab = saved.SelectedTab == ended ? tabs.FirstOrDefault()?.SessionHost : saved.SelectedTab };
         file.Write(JsonSerializer.Serialize(updated, Json));
      }
   }

   /// <summary>
   /// The file of <paramref name="project"/>'s window, named as Claude Code names a project's folder of transcripts: each
   /// character of its folder that is neither a letter nor a digit made a <c>-</c>. In lower case, since Windows' paths
   /// ignore case, as a <see cref="Project"/>'s equality does.
   /// </summary>
   FileWrittenWhole FileOf(Project project) =>
      new(Path.Combine(_folder.FullName, $"{new string([.. project.Folder.ToLowerInvariant().Select(it => char.IsAsciiLetterOrDigit(it) ? it : '-')])}.json"));
}
