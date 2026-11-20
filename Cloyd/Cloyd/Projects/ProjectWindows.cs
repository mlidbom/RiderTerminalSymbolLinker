using Avalonia.Controls;

namespace Cloyd.Projects;

/// <summary>
/// The <see cref="ProjectWindow"/>s the Cloyd application shows: one for each project, on the UI thread. A window
/// leaves them as it closes, and the project's next one opens anew.
/// </summary>
/// <param name="newWindowOf">Makes a new window of a project, not yet shown.</param>
sealed class ProjectWindows(Func<Project, ProjectWindow> newWindowOf)
{
   readonly Func<Project, ProjectWindow> _newWindowOf = newWindowOf;
   readonly List<ProjectWindow> _open = [];

   /// <summary>The windows open, in the order they opened.</summary>
   public IReadOnlyList<ProjectWindow> Open => _open;

   /// <summary>Shows the window of <paramref name="project"/>: brings it forward when it is open, else opens a new one.</summary>
   public ProjectWindow Show(Project project)
   {
      if(_open.SingleOrDefault(it => it.Project.Equals(project)) is {} open)
      {
         if(open.WindowState == WindowState.Minimized) open.WindowState = WindowState.Normal;
         open.Activate();
         return open;
      }

      var window = _newWindowOf(project);
      window.Closed += (_, _) => _open.Remove(window);
      _open.Add(window);
      window.Show();
      return window;
   }
}
