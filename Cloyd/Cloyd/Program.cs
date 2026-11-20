using Avalonia;
using Cloyd.LocalApi;
using Cloyd.Projects;

namespace Cloyd;

/// <summary>
/// Cloyd.exe, run in one of two ways:
/// <list type="bullet">
/// <item>
/// <c>Cloyd.exe --project &lt;folder&gt;</c>, the command, as an IDE runs it for a project it opens: has the Cloyd
/// application show the project's window, starting the application when none runs, and exits once the window shows.
/// The current directory's project when no folder is given.
/// </item>
/// <item>
/// <c>Cloyd.exe --application &lt;Cloyd's folder&gt;</c>, the Cloyd application: the one process on the machine that shows
/// every project's window, serving the <see cref="CloydsFolder"/> given. The command starts it.
/// </item>
/// </list>
/// </summary>
static class Program
{
   const string ArgumentThatMakesItTheApplication = "--application";

   [STAThread]
   public static int Main(string[] arguments) =>
      arguments is [ArgumentThatMakesItTheApplication, _]
         ? BuildAvaloniaApp().StartWithClassicDesktopLifetime(arguments)
         : ShowTheWindowOf(ProjectNamedBy(arguments));

   /// <summary>The application on the desktop. Also found by name by Avalonia's designer.</summary>
   public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<CloydApp>().UsePlatformDetect();

   /// <summary>The arguments that make Cloyd.exe the Cloyd application, serving <paramref name="folder"/>.</summary>
   public static string[] ArgumentsOfTheApplication(CloydsFolder folder) => [ArgumentThatMakesItTheApplication, folder.Folder.FullName];

   /// <summary>The folder the Cloyd application serves, as <paramref name="arguments"/>, those of the application, name it.</summary>
   public static CloydsFolder FolderOfTheApplication(string[] arguments) => new(new DirectoryInfo(arguments[1]));

   /// <summary>
   /// The command: has the Cloyd application show <paramref name="project"/>'s window. Exits with 0 once it shows, else
   /// with 1, telling why on the standard error, which whoever ran it may read.
   /// </summary>
   static int ShowTheWindowOf(Project project)
   {
      try
      {
         TheCloydApplication.OfCloydsEnvironment().ShowTheWindowOf(project);
         return 0;
      }
      catch(Exception failure) // The command's boundary, which tells whoever ran it why it failed.
      {
         Console.Error.WriteLine(failure);
         return 1;
      }
   }

   /// <summary>The project whose folder <c>--project &lt;folder&gt;</c> names: the current directory's when it isn't given.</summary>
   static Project ProjectNamedBy(string[] arguments)
   {
      var at = Array.IndexOf(arguments, "--project");
      return new Project(at >= 0 && at + 1 < arguments.Length ? arguments[at + 1] : Environment.CurrentDirectory);
   }
}
