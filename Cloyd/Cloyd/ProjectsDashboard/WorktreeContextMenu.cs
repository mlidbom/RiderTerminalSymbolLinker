using System.Diagnostics;
using Avalonia.Controls;
using Cloyd.Projects;
using CommunityToolkit.Mvvm.Input;

namespace Cloyd.ProjectsDashboard;

/// <summary>Git Extensions opens its browser or commit dialog in the worktree whose menu Magnus opened.</summary>
sealed class WorktreeContextMenu : ContextMenu
{
   public WorktreeContextMenu(Project checkout, Action<Exception> failed)
   {
      var open = new RelayCommand<ProcessStartInfo>(start =>
      {
         try { using var window = Process.Start(start!); }
         catch(Exception failure) { failed(new InvalidOperationException($"Could not open Git Extensions in {checkout.Folder}. {failure.Message}", failure)); }
      });
      Items.Add(new MenuItem { Header = "Git Extensions", Items = { Item("Browse", "browse"), Item("Commit", "commit") } });

      MenuItem Item(string label, string command) => new()
      {
         Header = label,
         Command = open,
         CommandParameter = new ProcessStartInfo("GitExtensions.exe", [command]) { WorkingDirectory = checkout.Folder }
      };
   }
}
