using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace Cloyd.VirtualDesktops;

/// <summary>
/// A top-level window of Cloyd's own process as Windows knows it, by its handle, an <c>HWND</c>: for Windows' virtual
/// desktops, of which Avalonia knows nothing.
/// </summary>
/// <remarks>
/// Asks a new <see cref="IVirtualDesktopManager"/> each time, on the calling thread, which takes microseconds: so no
/// COM object outlives the thread that made it.
/// </remarks>
sealed record NativeWindow(nint Handle)
{
   /// <summary>
   /// The native window of <paramref name="window"/>: null for a window of Avalonia's headless platform, as the
   /// specifications' are, which Windows doesn't know.
   /// </summary>
   public static NativeWindow? Of(TopLevel window) => window.TryGetPlatformHandle() is {HandleDescriptor: "HWND"} handle ? new NativeWindow(handle.Handle) : null;

   /// <summary>
   /// Whether the window is on the virtual desktop shown, where Magnus is. Windows switches to the desktop of a window
   /// that comes to the foreground, so bringing forward a window on another desktop takes him there.
   /// </summary>
   public bool IsOnTheCurrentVirtualDesktop => Asked(manager => manager.IsWindowOnCurrentVirtualDesktop(Handle));

   /// <summary>The virtual desktop the window is on: null while it is on none, as before it shows.</summary>
   public VirtualDesktop? Desktop => Asked(manager => manager.GetWindowDesktopId(Handle)) is var desktop && desktop != Guid.Empty ? new VirtualDesktop(desktop) : null;

   /// <summary>Moves the window onto <paramref name="desktop"/>, without bringing it forward or switching Magnus to that desktop.</summary>
   public void MoveTo(VirtualDesktop desktop) => Asked(manager =>
   {
      manager.MoveWindowToDesktop(Handle, desktop.Id);
      return true;
   });

   static TAnswer Asked<TAnswer>(Func<IVirtualDesktopManager, TAnswer> asking)
   {
      // ReSharper disable once SuspiciousTypeConversion.Global: a COM object answers the cast through QueryInterface.
      var manager = (IVirtualDesktopManager)new VirtualDesktopManager();
      try
      {
         return asking(manager);
      }
      finally
      {
         Marshal.FinalReleaseComObject(manager);
      }
   }
}
