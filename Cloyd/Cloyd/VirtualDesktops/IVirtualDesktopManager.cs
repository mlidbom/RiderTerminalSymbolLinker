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

   /// <summary>Unused: declared for its place in the interface's table of methods, which the declaration must keep in order.</summary>
   // ReSharper disable once UnusedMember.Global
   Guid GetWindowDesktopId(nint topLevelWindow);

   void MoveWindowToDesktop(nint topLevelWindow, [MarshalAs(UnmanagedType.LPStruct)] Guid desktopId);
}

