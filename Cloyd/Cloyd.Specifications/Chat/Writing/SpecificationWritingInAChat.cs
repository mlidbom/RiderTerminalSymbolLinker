using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using Cloyd.Chat;
using Cloyd.Chat.Writing;
using Cloyd.Projects;
using Cloyd.Sessions;
using Cloyd.Specifications.TheSandbox;

namespace Cloyd.Specifications.Chat.Writing;

/// <summary>
/// A specification of what Magnus writes in a chat's <see cref="InputBoxOfTheChat"/>, to the stand-in for Claude Code
/// that runs in the tab's host, through the sandbox's hub.
/// </summary>
public abstract class SpecificationWritingInAChat : SpecificationOpeningWindows
{
   /// <summary>
   /// A window of <paramref name="project"/> with a tab of a new session, whose stand-in has started, switched to its chat
   /// with Ctrl+H, which gives the chat's input box the keyboard.
   /// </summary>
   private protected (ProjectWindow Window, SessionTab Tab) AChatOfANewSessionIn(SandboxProject project)
   {
      var window = Open(project.Window);
      DoneOnTheUIThread(() => window.StartANewSession(name: null));
      var tab = OnTheUIThread(() => window.Tabs.Single());
      WaitUntil(() => tab.SessionRunningInIt is not null && tab.Terminal.TextShown.Contains("New session"), "the tab's terminal showed the stand-in's start");
      Focus(tab.Terminal);
      SwitchWithCtrlH(window, tab);
      return (window, tab);
   }

   /// <summary>
   /// Presses Ctrl+H in <paramref name="window"/>, which switches <paramref name="tab"/> between its terminal and its
   /// chat, and waits until the one it shows has the keyboard: the chat's input box, or the terminal.
   /// </summary>
   private protected void SwitchWithCtrlH(Window window, SessionTab tab)
   {
      var toTheChat = !OnTheUIThread(() => tab.Chat.IsVisible);
      Press(window, Key.H, PhysicalKey.H, RawInputModifiers.Control);
      WaitUntil(() => TopLevel.GetTopLevel(window)!.FocusManager!.GetFocusedElement() is Control focused
                      && (toTheChat ? focused.FindAncestorOfType<InputBoxOfTheChat>() is not null : focused == tab.Terminal || tab.Terminal.IsVisualAncestorOf(focused)),
                toTheChat ? "the chat's input box had the keyboard" : "the terminal had the keyboard");
   }

   /// <summary>Types <paramref name="text"/> into the chat's input box of <paramref name="window"/>, which has the keyboard, and presses Ctrl+Enter.</summary>
   protected static void TypeAndSend(Window window, string text)
   {
      Type(window, text);
      PressCtrlEnter(window);
   }

   protected static void PressCtrlEnter(Window window) => Press(window, Key.Enter, PhysicalKey.Enter, RawInputModifiers.Control);

   /// <summary>Puts what <paramref name="putOnTheClipboard"/> puts on the clipboard, and presses Ctrl+V in <paramref name="window"/>.</summary>
   protected static void PasteWithCtrlV(Window window, Func<IClipboard, Task> putOnTheClipboard)
   {
      OnTheUIThread(() => putOnTheClipboard(TopLevel.GetTopLevel(window)!.Clipboard!).GetAwaiter().GetResult());
      Press(window, Key.V, PhysicalKey.V, RawInputModifiers.Control);
   }

   /// <summary>What the chat of <paramref name="tab"/> shows now.</summary>
   private protected static IReadOnlyList<ShownInTheChat> ShownIn(SessionTab tab) => OnTheUIThread(() => tab.Chat.Shown.ToList());

   /// <summary>The prompts the chat of <paramref name="tab"/> shows now, in order.</summary>
   private protected static IReadOnlyList<string> PromptsIn(SessionTab tab) => [.. ShownIn(tab).OfType<ShownInTheChat.Prompt>().Select(it => it.Text)];

   /// <summary>What Claude wrote, as the chat of <paramref name="tab"/> shows it now, in order.</summary>
   private protected static IReadOnlyList<string> RepliesIn(SessionTab tab) => [.. ShownIn(tab).OfType<ShownInTheChat.ClaudesText>().Select(it => it.Markdown)];

   /// <summary>What the chat's input box of <paramref name="tab"/> holds now, as text.</summary>
   private protected static string TextIn(SessionTab tab) => OnTheUIThread(() => tab.Chat.InputBox.Draft.Document.Text);
}
