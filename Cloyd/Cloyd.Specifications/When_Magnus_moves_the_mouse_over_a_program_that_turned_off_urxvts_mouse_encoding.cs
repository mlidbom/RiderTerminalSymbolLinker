using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Compze.Must;
using Compze.xUnitBDD;
using Cloyd.Sessions;
using Cloyd.Specifications.TheSandbox;

namespace Cloyd.Specifications;

/// <summary>
/// The mouse moved over a session's terminal while a program in it has the mouse, one that has turned off urxvt's mouse
/// encoding as crossterm does outside Windows (<see cref="ProgramTakingTheMouse"/>). Conhost passes that on to the
/// terminal, and in some of Magnus's Codex sessions each move typed text such as <c>CO!CL!CI!</c> into Codex's input box.
/// </summary>
public class When_Magnus_moves_the_mouse_over_a_program_that_turned_off_urxvts_mouse_encoding : SpecificationOpeningWindows
{
   /// <summary>How many cells of a line the mouse moves across, each move reported to the program.</summary>
   const int CellsMovedAcross = 10;

   readonly SessionTab _tab;

   public When_Magnus_moves_the_mouse_over_a_program_that_turned_off_urxvts_mouse_encoding()
   {
      var project = Sandbox.Project("the-project");
      project.StartASessionHost(project.LabelOfCloydsHosts);
      var window = Open(project.Window);
      WaitUntil(() => window.BringingBackTheTabs.IsCompleted, "the window had brought back its tabs");
      _tab = OnTheUIThread(() => window.Tabs.Single());
      SwitchWithCtrlH(window, _tab);
      WaitUntil(() => TheTerminalShows("PS "), "the terminal showed the shell's prompt");
      Type(window, ProgramTakingTheMouse.CommandLine);
      Press(window, Key.Enter, PhysicalKey.Enter, RawInputModifiers.None);
      WaitUntil(() => TheTerminalShows(ProgramTakingTheMouse.SaysItHasTheMouse), "the program had the mouse");
      MoveTheMouseAcrossALineOfTheTerminal(window);
      Type(window, "q");
      WaitUntil(() => WhatCameToTheProgram is not null, "the program said what came to it", () => OnTheUIThread(() => _tab.Terminal.TextShown));
      Render(window, "Mouse-moved-over-a-program-that-turned-off-urxvts-mouse-encoding");
   }

   [XF] public void the_program_gets_the_moves_as_events_of_the_mouse() => WhatCameToTheProgram!.EventsOfTheMouse.Must().BeGreaterThan(0);

   [XF] public void the_program_gets_no_keys_from_them() => WhatCameToTheProgram!.Keys.Must().Be("");

   ProgramTakingTheMouse.WhatCame? WhatCameToTheProgram => OnTheUIThread(() => ProgramTakingTheMouse.WhatCameToItShownIn(_tab.Terminal));

   bool TheTerminalShows(string text) => OnTheUIThread(() => _tab.Terminal.TextShown.Contains(text));

   void MoveTheMouseAcrossALineOfTheTerminal(Window window) => OnTheUIThread(() =>
   {
      var topLeft = _tab.Terminal.TranslatePoint(default, window)!.Value;
      for(var cell = 0; cell < CellsMovedAcross; cell++) window.MouseMove(topLeft + new Point(20 + cell * 12, 40));
   });
}
