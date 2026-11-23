using System.Diagnostics;
using Avalonia.Controls;
using Cloyd.Projects;
using CommunityToolkit.Mvvm.Input;

namespace Cloyd.ProjectsDashboard;

/// <summary>The worktree menu opens the review window immediately. Git and the hub are queried by that window.</summary>
sealed class WorktreeContextMenu : ContextMenu
{
   protected override Type StyleKeyOverride => typeof(ContextMenu);

   public WorktreeContextMenu(DashboardWorktree worktree, Action<Exception> failed, Action review)
   {
      Items.Add(new MenuItem { Header = "Git Extensions", Items = { Item("Browse", "browse"), Item("Commit", "commit") } });
      Items.Add(new MenuItem { Header = "Remove worktree…", Command = new RelayCommand(review) });
      MenuItem Item(string label, string command) => new() { Header = label, CommandParameter = GitExtensions(worktree.StartFolder, command), Command = new RelayCommand<ProcessStartInfo>(start =>
      {
         try { using var window = Process.Start(start!); }
         catch(Exception failure) { failed(new InvalidOperationException($"Could not open Git Extensions in {worktree.StartFolder}. {failure.Message}", failure)); }
      }) };
   }

   public static ProcessStartInfo GitExtensions(Project checkout, string command) =>
      new("GitExtensions.exe", [command]) { WorkingDirectory = checkout.Folder };

   public static void OpenGitExtensions(Project checkout, string command)
   {
      using var window = Process.Start(GitExtensions(checkout, command));
   }
}
