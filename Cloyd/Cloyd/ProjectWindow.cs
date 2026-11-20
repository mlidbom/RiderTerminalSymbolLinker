using Avalonia.Controls;

namespace Cloyd;

/// <summary>
/// Cloyd's window for one project, beside the project's IDE window: the project's Claude Code sessions, each in a tab of
/// its own. Magnus, 2026-11-20: a window per project, "for now".
/// </summary>
sealed class ProjectWindow : Window
{
   public ProjectWindow(DirectoryInfo project)
   {
      Project = project;
      Title = $"Cloyd: {project.Name}";
      Width = 1400;
      Height = 900;
   }

   /// <summary>The folder of the project whose sessions the window shows.</summary>
   public DirectoryInfo Project { get; }
}
