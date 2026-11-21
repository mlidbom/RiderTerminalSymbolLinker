using AvaloniaEdit.Document;

namespace Cloyd.Chat.Writing;

/// <summary>
/// The message Magnus writes in the chat's input box, until he sends it: its text, the editor's document, with each
/// stretch of it he pasted marked (<see cref="PastedStretch"/>), so that its <see cref="Parts"/> are told apart as they
/// came in, typed, pasted, or an image, as Claude Code tells them apart in its own box.
/// </summary>
/// <remarks>
/// Where he pastes inside what he pasted, the innermost paste decides; two pastes side by side stay two, as in Claude
/// Code's box.
/// </remarks>
sealed class DraftOfAMessage
{
   /// <summary>What goes between a message put back into the draft and what the draft held.</summary>
   const string BetweenMessages = "\n\n";

   readonly TextSegmentCollection<PastedStretch> _pasted;

   /// <summary>How many images were pasted since the draft was last empty: the number of the next one's placeholder is one more.</summary>
   int _imagesPasted;

   public DraftOfAMessage()
   {
      _pasted = new TextSegmentCollection<PastedStretch>(Document);
      Document.TextChanged += (_, _) => Changed?.Invoke();
   }

   /// <summary>Raised when the draft has changed: its text, or what of it is marked pasted.</summary>
   public event Action? Changed;

   /// <summary>The draft's text, as the input box's editor edits it.</summary>
   public TextDocument Document { get; } = new();

   /// <summary>Whether it holds nothing to send: no text but white space.</summary>
   public bool IsEmpty => string.IsNullOrWhiteSpace(Document.Text);

   /// <summary>Its parts, in order: each stretch as it came in, typed text side by side merged.</summary>
   public IReadOnlyList<PartOfAMessage> Parts => Merged(PartsBetween(0, Document.TextLength, ancestors: []));

   /// <summary>The images it holds, each by its placeholder, in the order of the text: those whose placeholder is whole.</summary>
   public IReadOnlyList<PastedStretch.OfAnImage> Images => [.. _pasted.OfType<PastedStretch.OfAnImage>().Where(it => it.IsWholeIn(Document)).OrderBy(it => it.StartOffset)];

   /// <summary>Pastes <paramref name="text"/> in place of <paramref name="replacing"/>, such as the selection, and marks it pasted.</summary>
   public void Paste(string text, ISegment replacing)
   {
      Document.Replace(replacing, text);
      Mark(new PastedStretch.OfText(), replacing.Offset, text.Length);
   }

   /// <summary>Pastes the image saved as <paramref name="image"/> in place of <paramref name="replacing"/>, as its placeholder, such as <c>[Image #1]</c>.</summary>
   public void Paste(FileInfo image, ISegment replacing)
   {
      var placeholder = $"[Image #{++_imagesPasted}]";
      Document.Replace(replacing, placeholder);
      Mark(new PastedStretch.OfAnImage(image, placeholder), replacing.Offset, placeholder.Length);
   }

   /// <summary>The message the draft holds, which it then lets go of: it is empty again.</summary>
   public MessageFromTheChat TakeTheMessage()
   {
      var message = new MessageFromTheChat(Parts);
      Clear();
      return message;
   }

   /// <summary>
   /// Puts <paramref name="parts"/> back ahead of what the draft holds, a blank line between, as those of a message
   /// Claude Code didn't get: each as it came in. An image whose file is gone comes back as its placeholder's text.
   /// </summary>
   public void PutBackAhead(IReadOnlyList<PartOfAMessage> parts)
   {
      var at = 0;
      foreach(var part in parts) at += Insert(part, at);
      if(at > 0 && at < Document.TextLength) Document.Insert(at, BetweenMessages);
   }

   void Clear()
   {
      _pasted.Clear();
      Document.Text = "";
      _imagesPasted = 0;
   }

   /// <summary>Inserts <paramref name="part"/> at <paramref name="at"/>, as it came in, and returns how long it is in the text.</summary>
   int Insert(PartOfAMessage part, int at)
   {
      var where = new TextSegment { StartOffset = at, Length = 0 };
      switch(part)
      {
         case PartOfAMessage.TypedText typed:
            Document.Insert(at, typed.Text);
            return typed.Text.Length;
         case PartOfAMessage.PastedText pasted:
            Paste(pasted.Text, where);
            return pasted.Text.Length;
         case PartOfAMessage.PastedImage image when image.IsStillThere:
            var before = Document.TextLength;
            Paste(image.File, where);
            return Document.TextLength - before;
         case PartOfAMessage.PastedImage:
            var gone = $"[Image #{++_imagesPasted}]";
            Document.Insert(at, gone);
            return gone.Length;
         default:
            throw new ArgumentOutOfRangeException(nameof(part), part, "The draft can't hold it.");
      }
   }

   void Mark(PastedStretch stretch, int offset, int length)
   {
      stretch.StartOffset = offset;
      stretch.Length = length;
      _pasted.Add(stretch);
      Changed?.Invoke();
   }

   /// <summary>
   /// The parts of the text from <paramref name="from"/> to <paramref name="to"/>, which lies inside each of
   /// <paramref name="ancestors"/>, the stretches pasted around it, the innermost last: the text outside the stretches
   /// within it is of the kind of the innermost ancestor, pasted text, or typed where there is none.
   /// </summary>
   IEnumerable<PartOfAMessage> PartsBetween(int from, int to, IReadOnlyList<PastedStretch> ancestors)
   {
      var at = from;
      foreach(var stretch in OutermostBetween(from, to, ancestors))
      {
         if(stretch.StartOffset > at) yield return Outside(TextBetween(at, stretch.StartOffset));
         foreach(var part in PartsOf(stretch)) yield return part;
         at = stretch.EndOffset;
      }

      if(to > at) yield return Outside(TextBetween(at, to));

      PartOfAMessage Outside(string text) => ancestors is [.., PastedStretch.OfText] ? new PartOfAMessage.PastedText(text) : new PartOfAMessage.TypedText(text);

      IEnumerable<PartOfAMessage> PartsOf(PastedStretch stretch) =>
         stretch is PastedStretch.OfAnImage image && image.IsWholeIn(Document)
            ? [new PartOfAMessage.PastedImage(image.File)]
            : PartsBetween(stretch.StartOffset, stretch.EndOffset, [.. ancestors, stretch]);
   }

   /// <summary>
   /// The text from <paramref name="from"/> to <paramref name="to"/>, each line break a line feed, as the hub takes them:
   /// the editor breaks a line Magnus types with Windows' CR LF.
   /// </summary>
   string TextBetween(int from, int to) => Document.GetText(from, to - from).ReplaceLineEndings("\n");

   /// <summary>The stretches from <paramref name="from"/> to <paramref name="to"/> that lie in no other there, in order: none of <paramref name="ancestors"/>, nor an empty one.</summary>
   IEnumerable<PastedStretch> OutermostBetween(int from, int to, IReadOnlyList<PastedStretch> ancestors)
   {
      var after = from;
      foreach(var stretch in _pasted.FindOverlappingSegments(from, to - from)
                                    .Where(it => it.Length > 0 && it.StartOffset >= from && it.EndOffset <= to && !ancestors.Contains(it))
                                    .OrderBy(it => it.StartOffset)
                                    .ThenByDescending(it => it.Length))
      {
         if(stretch.StartOffset < after) continue;
         after = stretch.EndOffset;
         yield return stretch;
      }
   }

   /// <summary><paramref name="parts"/> with typed text side by side merged into one, and no empty text.</summary>
   static IReadOnlyList<PartOfAMessage> Merged(IEnumerable<PartOfAMessage> parts)
   {
      var merged = new List<PartOfAMessage>();
      foreach(var part in parts)
      {
         if(part is PartOfAMessage.TypedText { Text.Length: 0 } or PartOfAMessage.PastedText { Text.Length: 0 }) continue;
         if(part is PartOfAMessage.TypedText typed && merged is [.., PartOfAMessage.TypedText before])
            merged[^1] = new PartOfAMessage.TypedText(before.Text + typed.Text);
         else merged.Add(part);
      }

      return merged;
   }
}
