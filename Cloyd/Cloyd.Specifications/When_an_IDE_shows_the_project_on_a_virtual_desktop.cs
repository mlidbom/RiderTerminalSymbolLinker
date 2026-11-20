using System.Diagnostics;
using Compze.Must;
using Compze.xUnitBDD;
using Cloyd.Projects;
using Cloyd.Specifications.TheSandbox;
using Cloyd.VirtualDesktops;

namespace Cloyd.Specifications;

/// <summary>
/// An IDE shows the project in a window on a virtual desktop, as Magnus keeps each IDE window on a desktop of its own, and
/// the project's window opens in Cloyd with no desktop saved, as on its first opening. On Avalonia's headless platform no
/// window is on a desktop: what is specified is the desktop the window went to, <see cref="ProjectWindow.Desktop"/>, and
/// the one it saved.
/// </summary>
public class When_an_IDE_shows_the_project_on_a_virtual_desktop : SpecificationOpeningWindows
{
   readonly SandboxProject _project;
   readonly StandInIde _ide;
   readonly VirtualDesktop _desktopOfTheIdesWindow = new(Guid.NewGuid());
   readonly ProjectWindow _window;

   public When_an_IDE_shows_the_project_on_a_virtual_desktop()
   {
      _project = Sandbox.Project("the-project");
      _ide = EndWithTheScenario(StandInIde.In(Sandbox, "the-ide"));
      _ide.Shows(_project.Project, _desktopOfTheIdesWindow);
      _window = Open(_project.Window);
   }

   [XF] public void it_tells_of_no_failure()
   {
      WaitUntilTheWindowWentToTheDesktopOfTheIdesWindow();
      OnTheUIThread(() => _window.Failure).Must().BeNull();
   }

   void WaitUntilTheWindowWentToTheDesktopOfTheIdesWindow() =>
      WaitUntil(() => _window.Desktop == _desktopOfTheIdesWindow, "the window went to the desktop of the IDE's window");

   void WaitUntilTheWindowSavedTheDesktopOfTheIdesWindow() =>
      WaitUntil(() => _project.SavedWindow.Desktop == _desktopOfTheIdesWindow, "the window saved the desktop of the IDE's window");

   /// <summary>Waits as long as the window would take to look for its IDE's window again, if it did, as it did every 2 s when it followed it.</summary>
   void WaitAsLongAsTheWindowWouldTakeToLookAgain()
   {
      var clock = Stopwatch.StartNew();
      WaitUntil(() => clock.Elapsed > TimeSpan.FromSeconds(3), "the window would have looked for the IDE's window again");
   }

   public class and_the_IDEs_window_stays_there : When_an_IDE_shows_the_project_on_a_virtual_desktop
   {
      [XF] public void the_window_goes_to_that_desktop() => WaitUntilTheWindowWentToTheDesktopOfTheIdesWindow();

      [XF] public void the_window_saves_that_desktop_with_its_tabs() => WaitUntilTheWindowSavedTheDesktopOfTheIdesWindow();
   }

   public class and_the_IDEs_window_goes_to_another_desktop : When_an_IDE_shows_the_project_on_a_virtual_desktop
   {
      readonly VirtualDesktop _anotherDesktop = new(Guid.NewGuid());

      public and_the_IDEs_window_goes_to_another_desktop()
      {
         WaitUntilTheWindowWentToTheDesktopOfTheIdesWindow();
         _ide.Shows(_project.Project, _anotherDesktop);
         WaitAsLongAsTheWindowWouldTakeToLookAgain();
      }

      [XF] public void the_window_stays_on_the_desktop_it_went_to() => OnTheUIThread(() => _window.Desktop).Must().Be(_desktopOfTheIdesWindow);

      public class and_the_projects_window_opens_again : and_the_IDEs_window_goes_to_another_desktop
      {
         readonly ProjectWindow _windowAgain;

         public and_the_projects_window_opens_again()
         {
            WaitUntilTheWindowSavedTheDesktopOfTheIdesWindow();
            Close(_window);
            _windowAgain = Open(_project.Window);
            WaitAsLongAsTheWindowWouldTakeToLookAgain();
         }

         [XF] public void it_goes_back_to_the_desktop_it_was_on_not_to_the_IDEs_windows() => OnTheUIThread(() => _windowAgain.Desktop).Must().Be(_desktopOfTheIdesWindow);

         [XF] public void it_tells_of_no_failure_either() => OnTheUIThread(() => _windowAgain.Failure).Must().BeNull();
      }
   }
}
