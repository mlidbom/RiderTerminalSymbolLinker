using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Cloyd.Projects;
using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat;

/// <summary>
/// Magnus presses Ctrl+H in a session's tab that shows its terminal, which has the keyboard: the tab switches to its chat.
/// </summary>
public class When_Magnus_presses_Ctrl_H_in_a_sessions_tab : SpecificationOpeningWindows
{
   readonly ProjectWindow _window;
   readonly SessionTab _tab;

   public When_Magnus_presses_Ctrl_H_in_a_sessions_tab()
   {
      var project = Sandbox.Project("the-project");
      _window = Open(project.Window);
      DoneOnTheUIThread(() => _window.StartANewSession(name: null));
      _tab = OnTheUIThread(() => _window.Tabs.Single());
      WaitUntil(() => _tab.SessionRunningInIt is not null, "Claude Code ran in the tab's host");
      Focus(_tab.Terminal);
      PressCtrlH();
   }

   void PressCtrlH() => Press(_window, Key.H, PhysicalKey.H, RawInputModifiers.Control);

   (bool Chat, bool Terminal) Shown => OnTheUIThread(() => (_tab.Chat.IsVisible, _tab.Terminal.IsVisible));

   /// <summary>Waits until the control that has the keyboard is <paramref name="control"/>, or is in it.</summary>
   void WaitUntilItHasTheKeyboard(Control control, string what) =>
      WaitUntil(() => TopLevel.GetTopLevel(_window)!.FocusManager!.GetFocusedElement() is Visual focused && (focused == control || control.IsVisualAncestorOf(focused)), what);

   [XF] public void the_tab_shows_its_chat_and_not_its_terminal() => Shown.Must().Be((true, false));

   [XF] public void the_chat_has_the_keyboard() => WaitUntilItHasTheKeyboard(_tab.Chat, "the chat had the keyboard");

   public class and_presses_it_again : When_Magnus_presses_Ctrl_H_in_a_sessions_tab
   {
      public and_presses_it_again() => PressCtrlH();

      [XF] public void the_tab_shows_its_terminal_again_and_not_its_chat() => Shown.Must().Be((false, true));

      [XF] public void the_terminal_has_the_keyboard_again() => WaitUntilItHasTheKeyboard(_tab.Terminal, "the terminal had the keyboard again");
   }
}
