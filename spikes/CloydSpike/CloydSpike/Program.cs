using Avalonia;

namespace CloydSpike;

static class Program
{
   [STAThread]
   public static void Main(string[] arguments) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(arguments);

   public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<CloydApp>().UsePlatformDetect();
}
