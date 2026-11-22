using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Cloyd.Chat.Writing;
using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Writing;

/// <summary>
/// Magnus types into a chat's input box, pastes text with Ctrl+V, types more, and pastes an image, as a screenshot puts
/// one on the clipboard, with no text: each part is kept as it came in, as Claude Code keeps them in its own box.
/// </summary>
public class When_Magnus_pastes_into_a_chats_input_box : SpecificationWritingInAChat
{
   readonly Window _window;
   readonly SessionTab _tab;

   public When_Magnus_pastes_into_a_chats_input_box() => (_window, _tab) = Once(() =>
   {
      var (window, tab) = AChatOfANewSessionIn(Sandbox.Project("the-project"));
      Type(window, "Look at ");
      PasteWithCtrlV(window, clipboard => clipboard.SetTextAsync("these words"));
      Type(window, " and ");
      PasteWithCtrlV(window, clipboard => clipboard.SetBitmapAsync(AnImage()));
      WaitUntil(() => TextIn(tab).EndsWith("[Image #1]", StringComparison.Ordinal), "the box showed the image's placeholder");
      Render(window, "text-and-an-image-pasted-into-a-chats-input-box");
      return ((Window)window, tab);
   });

   IReadOnlyList<PartOfAMessage> Parts => OnTheUIThread(() => _tab.ClaudeCodeChat.InputBox.Draft.Parts);

   [XF] public void the_box_shows_the_image_as_its_placeholder() => TextIn(_tab).Must().Be("Look at these words and [Image #1]");

   [XF] public void it_keeps_what_was_typed_and_what_was_pasted_apart() =>
      Parts.Select(it => it.GetType().Name).Must().Satisfy(it => it.SequenceEqual([nameof(PartOfAMessage.TypedText), nameof(PartOfAMessage.PastedText), nameof(PartOfAMessage.TypedText), nameof(PartOfAMessage.PastedImage)]));

   [XF] public void the_image_is_saved_as_a_png_file() => Parts.OfType<PartOfAMessage.PastedImage>().Single().File.Must().Satisfy(it => it.Exists && it.Extension == ".png");

   [XF] public void a_thumbnail_of_the_image_shows_over_the_box() => OnTheUIThread(() => _tab.ClaudeCodeChat.InputBox.Thumbnails).Must().Satisfy(it => it.SequenceEqual(["[Image #1]"]));

   public class and_sends_it : When_Magnus_pastes_into_a_chats_input_box
   {
      public and_sends_it() => Once(() =>
      {
         PressCtrlEnter(_window);
         WaitUntil(() => PromptsIn(_tab).Count > 0, "the chat showed the message");
      });

      [XF] public void the_chat_shows_the_message_with_its_image() => PromptsIn(_tab).Must().Satisfy(it => it.SequenceEqual(["Look at these words and [Image #1]"]));

      [XF] public void no_thumbnail_shows_over_the_box() => OnTheUIThread(() => _tab.ClaudeCodeChat.InputBox.Thumbnails).Must().BeEmpty();
   }

   static Bitmap AnImage() => new WriteableBitmap(new PixelSize(8, 8), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
}
