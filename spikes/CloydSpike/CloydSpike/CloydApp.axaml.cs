using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace CloydSpike;

/// <summary>The spike's Cloyd: one window, for the project the command line names, or for every session.</summary>
sealed class CloydApp : Application
{
   public override void Initialize() => AvaloniaXamlLoader.Load(this);

   public override void OnFrameworkInitializationCompleted()
   {
      if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
         desktop.MainWindow = new CloydWindow(ValueOf("--project", desktop.Args ?? []) is { } project ? new DirectoryInfo(project) : null,
                                              ValueOf("--show-tui", desktop.Args ?? []));
      base.OnFrameworkInitializationCompleted();
   }

   /// <summary>
   /// What follows <paramref name="option"/> on the command line: null when it isn't there. <c>--project &lt;folder&gt;</c>
   /// makes a window of the sessions of that project, not of every session. <c>--show-tui &lt;candidate&gt;</c> opens the
   /// window without taking the keyboard, and shows the throwaway session's TUI in the candidate its name starts.
   /// </summary>
   static string? ValueOf(string option, string[] arguments)
   {
      var at = Array.IndexOf(arguments, option);
      return at >= 0 && at + 1 < arguments.Length ? arguments[at + 1] : null;
   }
}
