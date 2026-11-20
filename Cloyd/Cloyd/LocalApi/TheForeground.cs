using System.Runtime.InteropServices;

namespace Cloyd.LocalApi;

/// <summary>
/// Windows' foreground: the window in front, which gets what is typed. Windows lets a process bring a window of its own
/// there only in some cases, such as when it was started by the process in front, or one that may lets it.
/// </summary>
/// <remarks>
/// So the Cloyd application, which runs on its own, may not bring a project's window in front of the IDE that opened the
/// project, while <c>Cloyd.exe --project</c>, which the IDE in front runs, may: the command lets the application, as it
/// hands it the project. A window that may not come forward flashes its taskbar button instead.
/// </remarks>
static partial class TheForeground
{
   /// <summary>
   /// Lets the process <paramref name="processId"/> bring a window of its own to the foreground, when this process may.
   /// When it may not either, as when it was run by a process in the background, this changes nothing.
   /// </summary>
   public static void MayBeTakenBy(int processId) => _ = AllowSetForegroundWindow((uint)processId);

   [LibraryImport("user32.dll")]
   [return: MarshalAs(UnmanagedType.Bool)]
   private static partial bool AllowSetForegroundWindow(uint processId);
}
