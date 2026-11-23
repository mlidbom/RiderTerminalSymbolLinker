using System.Diagnostics;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Automation;
using Cloyd.Hub.Worktrees;
using Cloyd.Projects;
using CommunityToolkit.Mvvm.Input;

namespace Cloyd.ProjectsDashboard;

/// <summary>Git Extensions opens its browser or commit dialog in the worktree whose menu Magnus opened.</summary>
sealed class WorktreeContextMenu : ContextMenu
{
   protected override Type StyleKeyOverride => typeof(ContextMenu);

   public WorktreeContextMenu(DashboardWorktree worktree, Action<Exception> failed,
                              Func<CancellationToken, Task<WorktreeSessionsPreview>> preview, Action<WorktreeSessionsPreview> remove)
   {
      var checkout = worktree.StartFolder;
      var open = new RelayCommand<ProcessStartInfo>(start =>
      {
         try { using var window = Process.Start(start!); }
         catch(Exception failure) { failed(new InvalidOperationException($"Could not open Git Extensions in {checkout.Folder}. {failure.Message}", failure)); }
      });
      Items.Add(new MenuItem { Header = "Git Extensions", Items = { Item("Browse", "browse"), Item("Commit", "commit") } });
      WorktreeSessionsPreview? reviewed = null;
      CancellationTokenSource? reading = null;
      var removeReviewed = new RelayCommand(() => remove(reviewed!), () => reviewed is not null);
      var removal = new MenuItem { Header = "Remove worktree", Command = removeReviewed };
      ToolTip.SetShowOnDisabled(removal, true);
      Items.Add(removal);
      Opened += (_, _) =>
      {
         worktree.PropertyChanged += WorktreeChanged;
         RefreshRemoval();
      };
      Closed += (_, _) =>
      {
         worktree.PropertyChanged -= WorktreeChanged;
         reading?.Cancel();
      };

      void WorktreeChanged(object? sender, PropertyChangedEventArgs change)
      {
         if(change.PropertyName == nameof(worktree.RemovalBlocker)) RefreshRemoval();
      }

      async void RefreshRemoval()
      {
         reading?.Cancel();
         reviewed = null;
         removeReviewed.NotifyCanExecuteChanged();
         Explain(worktree.RemovalBlocker ?? "Checking sessions and shells in this worktree.");
         if(!worktree.CanRemove) return;
         using var current = new CancellationTokenSource();
         reading = current;
         try
         {
            var result = await preview(current.Token);
            current.Token.ThrowIfCancellationRequested();
            result.RequireReadyFor(checkout);
            reviewed = result;
            Explain("Remove this linked worktree");
            removeReviewed.NotifyCanExecuteChanged();
         }
         catch(OperationCanceledException) when(current.IsCancellationRequested) { }
         catch(Exception failure) { if(!current.IsCancellationRequested) Explain(failure.Message); }
         finally { if(reading == current) reading = null; }
      }

      void Explain(string text)
      {
         ToolTip.SetTip(removal, text);
         AutomationProperties.SetHelpText(removal, text);
      }

      MenuItem Item(string label, string command) => new()
      {
         Header = label,
         Command = open,
         CommandParameter = GitExtensions(checkout, command)
      };
   }

   public static ProcessStartInfo GitExtensions(Project checkout, string command) =>
      new("GitExtensions.exe", [command]) { WorkingDirectory = checkout.Folder };
}
