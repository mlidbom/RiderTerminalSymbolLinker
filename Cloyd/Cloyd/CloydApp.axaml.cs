using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Cloyd.Hub;
using Cloyd.Projects;
using Cloyd.Saving;
using Cloyd.Sessions;

namespace Cloyd;

/// <summary>Cloyd, started for one project: <c>Cloyd.exe --project &lt;folder&gt;</c> opens that project's window.</summary>
sealed class CloydApp : Application
{
   public override void Initialize() => AvaloniaXamlLoader.Load(this);

   public override void OnFrameworkInitializationCompleted()
   {
      if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
         desktop.MainWindow = new ProjectWindow(ProjectNamedBy(desktop.Args ?? []), TheHub.OfCloydsEnvironment(), ShellOfNewSessions.OnThisMachine(), SavedProjectWindows.OfCloydsEnvironment());
      base.OnFrameworkInitializationCompleted();
   }

   /// <summary>The project whose folder <c>--project &lt;folder&gt;</c> names: the current directory's when it isn't given.</summary>
   static Project ProjectNamedBy(string[] arguments)
   {
      var at = Array.IndexOf(arguments, "--project");
      return new Project(at >= 0 && at + 1 < arguments.Length ? arguments[at + 1] : Environment.CurrentDirectory);
   }
}
