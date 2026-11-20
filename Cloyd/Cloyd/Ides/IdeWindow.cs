using Cloyd.Projects;
using Cloyd.VirtualDesktops;

namespace Cloyd.Ides;

/// <summary>
/// A project's window in a JetBrains IDE, as the IDE's local API lists it: the project, and the virtual desktop the window
/// is on, null when it is on none.
/// </summary>
sealed record IdeWindow(Project Project, VirtualDesktop? Desktop);
