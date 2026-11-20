using Avalonia;
using Avalonia.Headless;

namespace Cloyd.Specifications;

/// <summary>Cloyd's Avalonia application on Avalonia's headless platform: drawn with Skia, on no screen.</summary>
static class HeadlessCloydApp
{
   /// <summary>Found by name by <see cref="HeadlessUnitTestSession.StartNew(System.Type,AvaloniaTestIsolationLevel)"/>.</summary>
   public static AppBuilder BuildAvaloniaApp() =>
      AppBuilder.Configure<CloydApp>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
