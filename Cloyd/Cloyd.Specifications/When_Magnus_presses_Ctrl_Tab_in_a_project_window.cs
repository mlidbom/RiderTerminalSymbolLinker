using Avalonia.Input;
using Cloyd.Projects;
using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

/// <summary>
/// Ctrl+Tab in a project window of three tabs, the first selected and its terminal with the keyboard: it selects the next
/// tab, as in a browser.
/// </summary>
public class When_Magnus_presses_Ctrl_Tab_in_a_project_window : SpecificationOpeningWindows
{
   readonly ProjectWindow _window;
   readonly IReadOnlyList<SessionTab> _tabs;

   public When_Magnus_presses_Ctrl_Tab_in_a_project_window()
   {
      var project = Sandbox.Project("the-project");
      for(var host = 0; host < 3; host++) project.StartASessionHost(project.LabelOfCloydsHosts);
      _window = Open(project.Window);
      WaitUntil(() => _window.BringingBackTheTabs.IsCompleted, "the window had brought back its tabs");
      _tabs = OnTheUIThread(() => _window.Tabs);
      _tabs.Count.Must().Be(3);
      Focus(OnTheUIThread(() => _window.SelectedTab!.Terminal));
      PressCtrlTab();
   }

   void PressCtrlTab() => Press(_window, Key.Tab, PhysicalKey.Tab, RawInputModifiers.Control);

   void PressCtrlShiftTab() => Press(_window, Key.Tab, PhysicalKey.Tab, RawInputModifiers.Control | RawInputModifiers.Shift);

   /// <summary>Which of the tabs is selected, counted from the first, 0.</summary>
   int SelectedTab => OnTheUIThread(() => _tabs.ToList().IndexOf(_window.SelectedTab!));

   [XF] public void the_second_tab_is_selected() => SelectedTab.Must().Be(1);

   public class and_presses_it_twice_more : When_Magnus_presses_Ctrl_Tab_in_a_project_window
   {
      public and_presses_it_twice_more()
      {
         PressCtrlTab();
         PressCtrlTab();
      }

      [XF] public void the_first_tab_is_selected_again_after_the_last() => SelectedTab.Must().Be(0);
   }

   public class and_then_Ctrl_Shift_Tab_twice : When_Magnus_presses_Ctrl_Tab_in_a_project_window
   {
      public and_then_Ctrl_Shift_Tab_twice()
      {
         PressCtrlShiftTab();
         PressCtrlShiftTab();
      }

      [XF] public void the_last_tab_is_selected_before_the_first() => SelectedTab.Must().Be(2);
   }
}
