using AvaloniaEdit.Document;

namespace Cloyd.Chat.Writing;

partial class PastedStretch
{
   /// <summary>
   /// The placeholder of an image Magnus pasted, such as <c>[Image #1]</c>, as Claude Code shows one in its box, which
   /// stands for the image saved as <paramref name="file"/>. Once he edits the placeholder, it is no longer the image's,
   /// and what is left of it is text.
   /// </summary>
   public sealed class OfAnImage(FileInfo file, string placeholder) : PastedStretch
   {
      /// <summary>The PNG file the image was saved as.</summary>
      public FileInfo File { get; } = file;

      /// <summary>The text that stands for the image in the draft, such as <c>[Image #1]</c>.</summary>
      public string Placeholder { get; } = placeholder;

      /// <summary>Whether <paramref name="document"/> holds the placeholder whole, unedited, where the stretch is.</summary>
      public bool IsWholeIn(TextDocument document) => Length == Placeholder.Length && document.GetText(this) == Placeholder;
   }
}
