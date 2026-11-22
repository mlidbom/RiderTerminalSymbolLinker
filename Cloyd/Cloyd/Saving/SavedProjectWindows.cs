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
   readonly Dictionary<string, SavedTabRestoration[]> _lastTabs = new(StringComparer.OrdinalIgnoreCase);
   long _restorationRevision;

   /// <summary>Changes on writes that affect the tabs a project window would restore. Routine saves
   /// of the desktop, selected tab or drafts leave an in-flight membership reading valid.</summary>
   public long RestorationRevision => Interlocked.Read(ref _restorationRevision);

   /// <summary>What was saved last of <paramref name="project"/>'s window: <see cref="SavedProjectWindow.NothingSaved"/> when nothing was.</summary>
   public SavedProjectWindow Of(Project project) =>
      FileOf(project).Text() is {} json ? JsonSerializer.Deserialize<SavedProjectWindow>(json, Json)! : SavedProjectWindow.NothingSaved;

   /// <summary>Replaces what is saved of <paramref name="project"/>'s window with <paramref name="window"/>, at once: a reader never finds half of it.</summary>
   public void Save(Project project, SavedProjectWindow window) => Write(FileNameOf(project), window);

   void Write(string fileName, SavedProjectWindow window)
   {
      new FileWrittenWhole(Path.Combine(_folder.FullName, fileName)).Write(JsonSerializer.Serialize(window, Json));
      var tabs = window.Tabs.Select(SavedTabRestoration.Of).ToArray();
      if(_lastTabs.TryGetValue(fileName, out var previous) && previous.SequenceEqual(tabs)) return;
      _lastTabs[fileName] = tabs;
      Interlocked.Increment(ref _restorationRevision);
   }

   /// <summary>Tabs of project windows that are closed. Compare the existing saved filenames
   /// directly so previous versions' files work even though the encoded paths are not reversible.</summary>
   public IEnumerable<SavedSessionTab> TabsOfClosedWindows(IEnumerable<Project> open)
   {
      if(!Directory.Exists(_folder.FullName)) yield break;
      var openFiles = open.Select(FileNameOf).ToHashSet(StringComparer.OrdinalIgnoreCase);
      foreach(var file in _folder.EnumerateFiles("*.json"))
      {
         if(openFiles.Contains(file.Name) || new FileWrittenWhole(file.FullName).Text() is not {} json) continue;
         foreach(var tab in JsonSerializer.Deserialize<SavedProjectWindow>(json, Json)!.Tabs) yield return tab;
      }
   }

   /// <summary>Ending a host in Projects Dashboard also removes its tab from closed project windows.
   /// A saved tab left there would resume the session in a new host when its project next opens.</summary>
   public void Forget(SessionHostId ended)
   {
      if(!Directory.Exists(_folder.FullName)) return;
      foreach(var entry in _folder.EnumerateFiles("*.json"))
      {
         var file = new FileWrittenWhole(entry.FullName);
         if(file.Text() is not {} json || JsonSerializer.Deserialize<SavedProjectWindow>(json, Json) is not {} saved) continue;
         if(!saved.Tabs.Any(it => it.SessionHost == ended)) continue;
         var tabs = saved.Tabs.Where(it => it.SessionHost != ended).ToArray();
         var updated = saved with { Tabs = tabs, SelectedTab = saved.SelectedTab == ended ? tabs.FirstOrDefault()?.SessionHost : saved.SelectedTab };
         Write(entry.Name, updated);
      }
   }

   /// <summary>
   /// The file of <paramref name="project"/>'s window, named as Claude Code names a project's folder of transcripts: each
   /// character of its folder that is neither a letter nor a digit made a <c>-</c>. In lower case, since Windows' paths
   /// ignore case, as a <see cref="Project"/>'s equality does.
   /// </summary>
   FileWrittenWhole FileOf(Project project) => new(Path.Combine(_folder.FullName, FileNameOf(project)));

   static string FileNameOf(Project project) => $"{new string([.. project.Folder.ToLowerInvariant().Select(it => char.IsAsciiLetterOrDigit(it) ? it : '-')])}.json";
}
