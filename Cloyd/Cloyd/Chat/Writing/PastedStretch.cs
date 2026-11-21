using AvaloniaEdit.Document;

namespace Cloyd.Chat.Writing;

/// <summary>
/// A stretch of a <see cref="DraftOfAMessage"/> that Magnus pasted: text, or an image's placeholder. The draft's
/// document moves it as he edits around it, and grows it as he types inside it.
/// </summary>
abstract partial class PastedStretch : TextSegment;
