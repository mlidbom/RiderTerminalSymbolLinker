using System.Diagnostics;
using Compze.Must;
using Compze.xUnitBDD;
using Cloyd.Projects;
using Cloyd.Specifications.TheSandbox;
using Cloyd.VirtualDesktops;

namespace Cloyd.Specifications;

/// <summary>
/// An IDE shows the project in a window on a virtual desktop, as Magnus keeps each IDE window on a desktop of its own, and
/// the project's window opens in Cloyd. On Avalonia's headless platform no window is on a desktop: what is specified is
/// the desktop the window went to, <see cref="ProjectWindow.DesktopOfItsIdesWindow"/>.
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
      WaitUntil(() => _window.DesktopOfItsIdesWindow is not null, "the window went to a desktop");
      OnTheUIThread(() => _window.Failure).Must().BeNull();
   }

   void WaitUntilTheWindowWentToTheDesktopOfTheIdesWindow() =>
      WaitUntil(() => _window.DesktopOfItsIdesWindow == _desktopOfTheIdesWindow, "the window went to the desktop of the IDE's window");

   public class and_the_IDEs_window_stays_there : When_an_IDE_shows_the_project_on_a_virtual_desktop
   {
      [XF] public void the_window_goes_to_that_desktop() => WaitUntilTheWindowWentToTheDesktopOfTheIdesWindow();
   }

   public class and_the_IDEs_window_goes_to_another_desktop : When_an_IDE_shows_the_project_on_a_virtual_desktop
   {
      readonly VirtualDesktop _anotherDesktop = new(Guid.NewGuid());

      public and_the_IDEs_window_goes_to_another_desktop()
      {
         WaitUntilTheWindowWentToTheDesktopOfTheIdesWindow();
         _ide.Shows(_project.Project, _anotherDesktop);
      }

      [XF] public void the_window_follows_it_there() =>
         WaitUntil(() => _window.DesktopOfItsIdesWindow == _anotherDesktop, "the window followed the IDE's window to the other desktop");
   }

   public class and_the_IDE_exits : When_an_IDE_shows_the_project_on_a_virtual_desktop
   {
      public and_the_IDE_exits()
      {
         WaitUntilTheWindowWentToTheDesktopOfTheIdesWindow();
         _ide.Exits();
         var clock = Stopwatch.StartNew();
         WaitUntil(() => clock.Elapsed > TimeSpan.FromSeconds(3), "the window had looked for the IDE's window again, which it does every 2 s");
      }

      [XF] public void the_window_stays_on_the_desktop_it_went_to() => OnTheUIThread(() => _window.DesktopOfItsIdesWindow).Must().Be(_desktopOfTheIdesWindow);
   }
}
