using Vanara.PInvoke;
using static Vanara.PInvoke.User32;
using VirtualDesktop = WindowsDesktop.VirtualDesktop;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Screen;

/// <summary>A window on the desktop, not inside another: a Rider frame, or one of its dialogs, which the frame owns.</summary>
sealed class TopLevelWindow
{
   readonly HWND _handle;

   TopLevelWindow(HWND handle, string title, int processId)
   {
      _handle = handle;
      Title = title;
      ProcessId = processId;
   }

   /// <summary>Every visible top-level window, on every virtual desktop, in the desktop's z-order, topmost first.</summary>
   /// <remarks>
   ///   The title and process are read as the windows are listed. A window that closes meanwhile, as Rider's splash
   ///   screen does, is left out instead of failing a later read.
   /// </remarks>
   public static IReadOnlyList<TopLevelWindow> AllVisible()
   {
      var windows = new List<TopLevelWindow>();
      EnumWindows((handle, _) =>
                  {
                     if(IsWindowVisible(handle) && GetWindowThreadProcessId(handle, out var processId) != 0)
                        windows.Add(new TopLevelWindow(handle, TitleOf(handle), (int)processId));
                     return true;
                  },
                  IntPtr.Zero);
      return windows;
   }

   /// <summary>The title the window had when it was listed.</summary>
   public string Title { get; }

   public int ProcessId { get; }

   /// <summary>Whether another window owns this one, as a Rider frame owns its dialogs.</summary>
   public bool IsOwnedByAnotherWindow => !GetWindow(_handle, GetWindowCmd.GW_OWNER).IsNull;

   /// <summary>The monitor that holds most of this window.</summary>
   public Monitor Monitor => Monitor.Holding(_handle);

   /// <summary>Where the window is, in physical pixels of the virtual screen.</summary>
   public RECT Bounds
   {
      get
      {
         GetWindowRect(_handle, out var bounds);
         return bounds;
      }
   }

   /// <summary>The virtual desktop the window is on, or null for a window pinned to all of them.</summary>
   public VirtualDesktop? Desktop => VirtualDesktop.FromHwnd((IntPtr)_handle);

   /// <summary>Moves this window to the virtual desktop <paramref name="other"/> is on.</summary>
   public void MoveToTheDesktopOf(TopLevelWindow other) =>
      VirtualDesktop.MoveToDesktop((IntPtr)_handle,
                                   other.Desktop ?? throw new InvalidOperationException($"'{other.Title}' is pinned to every virtual desktop, so it has no one desktop to move to."));

   /// <summary>The window's title, place and desktop, as the tools report them.</summary>
   public string Description
   {
      get
      {
         var bounds = Bounds;
         var desktopNumber = Desktop is {} desktop ? $"Desktop {Array.FindIndex(VirtualDesktop.GetDesktops(), it => it.Id == desktop.Id) + 1}" : "every desktop";
         return $"'{Title}' at ({bounds.left}, {bounds.top}), {bounds.Width}x{bounds.Height}, on {desktopNumber}";
      }
   }

   /// <summary>Restores the window if it's minimized or maximized, and gives it <paramref name="bounds"/>, in physical pixels.</summary>
   public void MoveTo(RECT bounds)
   {
      ShowWindow(_handle, ShowWindowCommand.SW_RESTORE);
      SetWindowPos(_handle,
                   HWND.NULL,
                   bounds.left,
                   bounds.top,
                   bounds.Width,
                   bounds.Height,
                   SetWindowPosFlags.SWP_NOZORDER | SetWindowPosFlags.SWP_NOACTIVATE);
   }

   /// <summary>Asks the window to close, as its close button does. The application decides what that means.</summary>
   public void AskToClose() => PostMessage(_handle, (uint)WindowMessage.WM_CLOSE);

   static string TitleOf(HWND handle)
   {
      var title = new System.Text.StringBuilder(GetWindowTextLength(handle) + 1);
      var length = GetWindowText(handle, title, title.Capacity);
      return title.ToString(0, length);
   }
}
