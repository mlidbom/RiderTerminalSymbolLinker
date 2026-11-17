using static Vanara.PInvoke.User32;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Screen;

/// <summary>
///   Runs work that talks to the Windows desktop on a thread of its own: in a single-threaded apartment, as the virtual
///   desktop COM API wants, and per-monitor DPI aware, so that window positions are physical pixels on every monitor.
/// </summary>
/// <remarks>The tools are called from threads that are neither, such as a test runner's.</remarks>
static class ScreenThread
{
   public static T Run<T>(Func<T> work)
   {
      var outcome = new TaskCompletionSource<T>();
      var thread = new Thread(() =>
      {
         SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
         try
         {
            outcome.SetResult(work());
         }
         catch(Exception exception) // Handed to the calling thread, which rethrows it below.
         {
            outcome.SetException(exception);
         }
      });
      thread.SetApartmentState(ApartmentState.STA);
      thread.Start();
      thread.Join();
      return outcome.Task.GetAwaiter().GetResult();
   }

   public static void Run(Action work) => Run(() =>
   {
      work();
      return true;
   });
}
