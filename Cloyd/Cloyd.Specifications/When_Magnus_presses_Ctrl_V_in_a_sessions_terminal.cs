using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Cloyd.Projects;
using Cloyd.Sessions;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

/// <summary>
/// Ctrl+V in a session's terminal, as Magnus pastes in every other Windows program: in Claude Code on Windows it did
/// nothing, and only Ctrl+Shift+V pasted.
/// </summary>
public abstract class When_Magnus_presses_Ctrl_V_in_a_sessions_terminal : SpecificationOpeningWindows
{
   readonly ProjectWindow _window;
   readonly SessionTab _tab;

   /// <summary>Opens a window on a new session, switched to its terminal with Ctrl+H, which then has the keyboard, puts what <paramref name="putOnTheClipboard"/> puts on the clipboard, and presses Ctrl+V.</summary>
   protected When_Magnus_presses_Ctrl_V_in_a_sessions_terminal(Func<IClipboard, Task> putOnTheClipboard)
   {
      var project = Sandbox.Project("the-project");
      _window = Open(project.Window);
      DoneOnTheUIThread(() => _window.StartANewSession(name: null));
      _tab = OnTheUIThread(() => _window.Tabs.Single());
      WaitUntil(() => _tab.SessionRunningInIt is not null && _tab.Terminal.TextShown.Contains("New session"), "the tab's terminal showed the stand-in's start");
      SwitchWithCtrlH(_window, _tab);
      OnTheUIThread(() => putOnTheClipboard(TopLevel.GetTopLevel(_window)!.Clipboard!).GetAwaiter().GetResult());
      Press(_window, Key.V, PhysicalKey.V, RawInputModifiers.Control);
   }

   string LinesOfTheInputBox => OnTheUIThread(() => InputBoxOfTheStandIn.LinesShownIn(_tab.Terminal));

   /// <summary>Waits until the stand-in's input box shows <paramref name="shown"/>, as it does once what was pasted reaches it.</summary>
   void WaitUntilTheInputBoxShows(string shown) => WaitUntil(() => LinesOfTheInputBox == shown, $"the input box showed {shown}");

   public class with_text_on_the_clipboard() : When_Magnus_presses_Ctrl_V_in_a_sessions_terminal(clipboard => clipboard.SetTextAsync("words from the clipboard"))
   {
      [XF] public void claude_codes_input_box_holds_the_text() => WaitUntilTheInputBoxShows("❯ words from the clipboard");
   }

   /// <summary>An image, as a screenshot puts on the clipboard: no text with it.</summary>
   public class with_an_image_and_no_text_on_the_clipboard() : When_Magnus_presses_Ctrl_V_in_a_sessions_terminal(clipboard => clipboard.SetBitmapAsync(AnImage()))
   {
      /// <summary>Claude Code shows it as an image, which it does only for an image file's path pasted alone: the PNG Cloyd saved it as.</summary>
      [XF] public void claude_codes_input_box_holds_the_image() => WaitUntilTheInputBoxShows("❯ [Image #1]");

      static Bitmap AnImage() => new WriteableBitmap(new PixelSize(8, 8), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
   }
}
