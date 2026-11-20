using Avalonia.Headless;
using Avalonia.Threading;

namespace Cloyd.Specifications;

/// <summary>
/// The one UI thread of the specifications' headless Avalonia session, which every specification of the assembly shares:
/// Cloyd's windows are made, shown, laid out, drawn and closed on it.
/// </summary>
static class TheHeadlessUIThread
{
   static readonly HeadlessUnitTestSession HeadlessSession = HeadlessUnitTestSession.StartNew(typeof(HeadlessCloydApp), AvaloniaTestIsolationLevel.PerAssembly);

   static readonly Thread ItsThread = HeadlessSession.Dispatch(() => Thread.CurrentThread, CancellationToken.None).GetAwaiter().GetResult();

   /// <summary>Runs <paramref name="onTheUIThread"/> on the UI thread, once the jobs posted to it before have run, and returns what it returns.</summary>
   public static TResult Run<TResult>(Func<TResult> onTheUIThread) =>
      Thread.CurrentThread == ItsThread
         ? onTheUIThread()
         : HeadlessSession.Dispatch(() =>
         {
            Dispatcher.UIThread.RunJobs();
            return onTheUIThread();
         }, CancellationToken.None).GetAwaiter().GetResult();

   /// <summary>Runs <paramref name="onTheUIThread"/> on the UI thread, once the jobs posted to it before have run.</summary>
   public static void Run(Action onTheUIThread) => Run(() =>
   {
      onTheUIThread();
      return true;
   });
}
