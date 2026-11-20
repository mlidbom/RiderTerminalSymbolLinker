using System.Runtime.InteropServices;

namespace Cloyd.VirtualDesktops;

/// <summary>
/// Windows' documented API of its virtual desktops, <c>IVirtualDesktopManager</c> of <c>shobjidl_core.h</c>: whether a
/// top-level window is on the desktop shown, which desktop it is on, and moving it to another, which it does only for the
/// calling process's own windows. Made as a <see cref="VirtualDesktopManager"/>.
/// </summary>
[ComImport, Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IVirtualDesktopManager
{
   [return: MarshalAs(UnmanagedType.Bool)]
   bool IsWindowOnCurrentVirtualDesktop(nint topLevelWindow);

   /// <summary>The desktop <paramref name="topLevelWindow"/> is on: <see cref="Guid.Empty"/> while it is on none, as before it shows.</summary>
   Guid GetWindowDesktopId(nint topLevelWindow);

   void MoveWindowToDesktop(nint topLevelWindow, [MarshalAs(UnmanagedType.LPStruct)] Guid desktopId);
}

