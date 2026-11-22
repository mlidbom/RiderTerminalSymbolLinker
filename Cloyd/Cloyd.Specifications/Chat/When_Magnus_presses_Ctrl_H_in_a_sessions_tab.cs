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
/// A new session's tab, which shows its chat at first, its input box with the keyboard; then Magnus presses Ctrl+H: the
/// tab switches to its terminal.
/// </summary>
public class When_Magnus_presses_Ctrl_H_in_a_sessions_tab : SpecificationOpeningWindows
{
   readonly ProjectWindow _window;
   readonly SessionTab _tab;
   readonly bool _showedTheChatAtFirst;

   public When_Magnus_presses_Ctrl_H_in_a_sessions_tab()
   {
      var project = Sandbox.Project("the-project");
      _window = Open(project.Window);
      DoneOnTheUIThread(() => _window.StartANewSession(new NewSession(AgentCli.ClaudeCode, Name: null)));
      _tab = OnTheUIThread(() => _window.Tabs.Single());
      WaitUntil(() => _tab.SessionRunningInIt is not null, "Claude Code ran in the tab's host");
      _showedTheChatAtFirst = ShowsTheChat;
      WaitUntilTheInputBoxHasTheKeyboard(_window, _tab);
      PressCtrlH();
   }

   void PressCtrlH() => Press(_window, Key.H, PhysicalKey.H, RawInputModifiers.Control);

   bool ShowsTheChat => OnTheUIThread(() => _tab.ShowsTheChat);

   /// <summary>Waits until the control that has the keyboard is <paramref name="control"/>, or is in it.</summary>
   void WaitUntilItHasTheKeyboard(Control control, string what) =>
      WaitUntil(() => TopLevel.GetTopLevel(_window)!.FocusManager!.GetFocusedElement() is Visual focused && (focused == control || control.IsVisualAncestorOf(focused)), what);

   [XF] public void at_first_the_tab_showed_its_chat() => _showedTheChatAtFirst.Must().BeTrue();

   [XF] public void the_tab_shows_its_terminal() => ShowsTheChat.Must().BeFalse();

   [XF] public void the_terminal_has_the_keyboard() => WaitUntilItHasTheKeyboard(_tab.Terminal, "the terminal had the keyboard");

   public class and_presses_it_again : When_Magnus_presses_Ctrl_H_in_a_sessions_tab
   {
      public and_presses_it_again() => PressCtrlH();

      [XF] public void the_tab_shows_its_chat_again() => ShowsTheChat.Must().BeTrue();

      [XF] public void the_chats_input_box_has_the_keyboard_again() => WaitUntilItHasTheKeyboard(_tab.ClaudeCodeChat.InputBox, "the chat's input box had the keyboard again");
   }
}
