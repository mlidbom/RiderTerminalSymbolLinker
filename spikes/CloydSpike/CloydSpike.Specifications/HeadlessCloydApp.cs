using Avalonia;
using Avalonia.Headless;

namespace CloydSpike.Specifications;

/// <summary>The spike's Avalonia application on Avalonia's headless platform: drawn with Skia, on no screen.</summary>
static class HeadlessCloydApp
{
   /// <summary>Found by name by <see cref="HeadlessUnitTestSession.StartNew(System.Type,AvaloniaTestIsolationLevel)"/>.</summary>
   public static AppBuilder BuildAvaloniaApp() =>
      AppBuilder.Configure<CloydApp>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

   public static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(typeof(HeadlessCloydApp), AvaloniaTestIsolationLevel.PerAssembly);

   /// <summary>Runs <paramref name="onTheUiThread"/> on the headless platform's UI thread, and returns what it returns.</summary>
   public static T OnTheUiThread<T>(Func<T> onTheUiThread) => Session.Dispatch(onTheUiThread, CancellationToken.None).GetAwaiter().GetResult();
}
