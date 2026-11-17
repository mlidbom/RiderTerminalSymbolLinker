using System.Diagnostics;
using Vanara.PInvoke;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Screen;

/// <summary>
///   Where the <see cref="Riders.SandboxRider"/>'s windows go: with the IDEA the plugin is developed in, on its virtual
///   desktop, but on another monitor, small and in the bottom-left corner.
/// </summary>
/// <remarks>
///   As the .NET Analysis&amp;Refactoring MCP's sandbox, where Magnus said, 2026-11-14: on the desktop of the IDE
///   Claude develops in, on the monitor that IDE isn't on, and small, so that what he keeps on that monitor, such as
///   Git Extensions, stays in reach.<br/>
///   A new Rider window opens on whichever virtual desktop is active, which needn't be that one, hence the move.
/// </remarks>
static class SandboxWindowPlacement
{
   const int Width = 1400;
   const int Height = 900;

   public static void Apply(IEnumerable<TopLevelWindow> sandboxWindows) =>
      ScreenThread.Run(() => ApplyOnThisThread(sandboxWindows));

   static void ApplyOnThisThread(IEnumerable<TopLevelWindow> sandboxWindows)
   {
      var developmentIdea = TheDevelopmentIdeaWindow();
      var monitor = Monitor.All().FirstOrDefault(it => developmentIdea is null || !it.IsTheSameAs(developmentIdea.Monitor))
                 ?? Monitor.All()[0];
      var workArea = monitor.WorkArea;
      var bounds = new RECT(workArea.left, workArea.bottom - Height, workArea.left + Width, workArea.bottom);

      foreach(var window in sandboxWindows)
      {
         if(developmentIdea is not null) window.MoveToTheDesktopOf(developmentIdea);
         window.MoveTo(bounds);
      }
   }

   /// <summary>The window of the IDEA the plugin is developed in: the one with this repository open, unless it's closed.</summary>
   public static TopLevelWindow? TheDevelopmentIdeaWindow() =>
      TopLevelWindow.AllVisible().FirstOrDefault(it => it.Title.StartsWith(RepositoryRoot.IdeaProjectName, StringComparison.Ordinal)
                                                    && Process.GetProcessById(it.ProcessId).ProcessName == "idea64");
}
