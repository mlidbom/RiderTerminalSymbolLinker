using Avalonia;

namespace Cloyd;

static class Program
{
   [STAThread]
   public static void Main(string[] arguments) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(arguments);

   /// <summary>The application on the desktop. Also found by name by Avalonia's designer.</summary>
   public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<CloydApp>().UsePlatformDetect();
}
