using Cloyd.Projects;
using Cloyd.VirtualDesktops;

namespace Cloyd.Ides;

/// <summary>
/// A window in <see cref="WindowsAnIdeListed"/>: the folder of its project, and the GUID of the virtual desktop it is on,
/// null when it is on none.
/// </summary>
sealed record WindowAnIdeListed(string Project, Guid? Desktop)
{
   public IdeWindow AsIdeWindow => new(new Project(Project), Desktop is {} desktop ? new VirtualDesktop(desktop) : null);
}
