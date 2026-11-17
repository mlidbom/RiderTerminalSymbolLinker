using Vanara.PInvoke;
using static Vanara.PInvoke.User32;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Screen;

/// <summary>A physical display, with the part of it windows may use: its work area, without the taskbar.</summary>
sealed class Monitor
{
   readonly HMONITOR _handle;

   Monitor(HMONITOR handle) => _handle = handle;

   /// <summary>Every monitor, left to right.</summary>
   public static IReadOnlyList<Monitor> All()
   {
      var monitors = new List<Monitor>();
      EnumDisplayMonitors(HDC.NULL,
                          null,
                          (handle, _, _, _) =>
                          {
                             monitors.Add(new Monitor(handle));
                             return true;
                          },
                          IntPtr.Zero);
      return [.. monitors.OrderBy(it => it.WorkArea.left)];
   }

   /// <summary>The monitor that holds most of <paramref name="window"/>.</summary>
   public static Monitor Holding(HWND window) => new(MonitorFromWindow(window, MonitorFlags.MONITOR_DEFAULTTONEAREST));

   /// <summary>The monitor's area without the taskbar, in physical pixels of the virtual screen.</summary>
   public RECT WorkArea
   {
      get
      {
         var information = new MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
         GetMonitorInfo(_handle, ref information);
         return information.rcWork;
      }
   }

   public bool IsTheSameAs(Monitor other) => _handle == other._handle;
}
