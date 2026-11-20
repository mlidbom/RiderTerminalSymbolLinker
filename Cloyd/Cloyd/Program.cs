using System.Diagnostics;
using Avalonia;
using Cloyd.DotNetAnalysisAndRefactoring;
using Cloyd.Links;
using Cloyd.LocalApi;
using Cloyd.Projects;

namespace Cloyd;

/// <summary>
/// Cloyd.exe, run in one of three ways:
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
/// <item>
/// <c>Cloyd.exe --links-in &lt;folder&gt; &lt;line&gt;</c>, a development tool: what links in the line of a terminal's
/// output, in the project's window (<see cref="TellWhatLinksIn"/>).
/// </item>
/// </list>
/// </summary>
static class Program
{
   const string ArgumentThatMakesItTheApplication = "--application";
   const string ArgumentThatAsksWhatLinks = "--links-in";

   [STAThread]
   public static int Main(string[] arguments) =>
      arguments switch
      {
         [ArgumentThatMakesItTheApplication, _] => BuildAvaloniaApp().StartWithClassicDesktopLifetime(arguments),
         [ArgumentThatAsksWhatLinks, var folder, var line] => TellWhatLinksIn(line, new Project(folder)),
         _ => ShowTheWindowOf(ProjectNamedBy(arguments))
      };

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

   /// <summary>
   /// A development tool: prints what links in <paramref name="line"/>, a line of a terminal's output, in
   /// <paramref name="project"/>'s window, with the project's files as git lists them now, and its C# symbols as Rider
   /// has them now. So what links, and what doesn't, can be checked against Magnus's projects and Rider, with no window.
   /// </summary>
   static int TellWhatLinksIn(string line, Project project)
   {
      var filesAndSymbols = new FilesAndSymbolsOfAProject(project, DotNetAnalysisAndRefactoringMcp.OnThisMachine());
      var started = Stopwatch.StartNew();
      filesAndSymbols.ReadTheFiles(CancellationToken.None).GetAwaiter().GetResult();
      Console.WriteLine($"Read the files of {project} in {started.ElapsedMilliseconds} ms.");
      started.Restart();
      Console.WriteLine(filesAndSymbols.GatherTheSymbols(CancellationToken.None).GetAwaiter().GetResult()
                           ? $"Gathered the symbols of its solutions in {started.ElapsedMilliseconds} ms."
                           : "Rider's .NET Analysis&Refactoring MCP didn't answer: no symbols.");
      foreach(var file in filesAndSymbols.FileReferencesIn(line)) Console.WriteLine(file);
      foreach(var symbol in filesAndSymbols.SymbolReferencesIn(line)) Console.WriteLine(symbol);
      return 0;
   }

   /// <summary>The project whose folder <c>--project &lt;folder&gt;</c> names: the current directory's when it isn't given.</summary>
   static Project ProjectNamedBy(string[] arguments)
   {
      var at = Array.IndexOf(arguments, "--project");
      return new Project(at >= 0 && at + 1 < arguments.Length ? arguments[at + 1] : Environment.CurrentDirectory);
   }
}
