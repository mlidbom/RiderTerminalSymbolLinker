using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Cloyd.Hub;
using Cloyd.Ides;
using Cloyd.LocalApi;
using Cloyd.Projects;
using Cloyd.Sessions;

namespace Cloyd;

/// <summary>
/// The Cloyd application: the one process on the machine that shows every project's window, which
/// <c>Cloyd.exe --project</c> hands each project to through its <see cref="CloydsLocalApi"/>. It starts with no window,
/// and ends as its last window closes.
/// </summary>
sealed class CloydApp : Application
{
   public override void Initialize() => AvaloniaXamlLoader.Load(this);

   public override void OnFrameworkInitializationCompleted()
   {
      if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
      {
         var folder = Program.FolderOfTheApplication(desktop.Args!);
         var hub = TheHub.OfCloydsEnvironment();
         var shell = ShellOfNewSessions.OnThisMachine();
         var ides = TheIdes.OnThisMachine();
         var windows = new ProjectWindows(project => new ProjectWindow(project, hub, ides, shell, folder.SavedProjectWindows));
         desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;
         var localApi = CloydsLocalApi.Start(windows, folder);
         desktop.Exit += (_, _) => localApi.Stop();
      }

      base.OnFrameworkInitializationCompleted();
   }
}
