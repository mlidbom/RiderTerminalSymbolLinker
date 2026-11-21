using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Cloyd.Chat.Views;
using Cloyd.Chat.Writing;
using Cloyd.ClaudeCode;
using Cloyd.Hub;
using Cloyd.Pasting;
using LiveMarkdown.Avalonia;
using static Cloyd.Chat.Views.LinesOfTheChat;

namespace Cloyd.Chat;

/// <summary>
/// The chat of a session: its conversation, as the hub's stream of it tells, drawn as the Claude desktop app's Code tab
/// draws one (<c>dev_docs/cloyd-chat-specification.md</c>) in a column down the middle: Magnus's prompts in bubbles at the
/// right, what Claude wrote across the column, its tool calls folded into grey lines. Under it, the
/// <see cref="InputBoxOfTheChat"/>, where Magnus writes to Claude.
/// </summary>
/// <remarks>
/// It follows the conversation only while it is shown, as a tab's selected view: the hub holds every message of a
/// conversation in memory while a stream follows it. Shown again, it reads the conversation anew, and draws only what
/// changed meanwhile.<br/>
/// A long conversation, such as one of Magnus's of 15,000 messages, is drawn only as far up as he scrolls: see
/// <see cref="DrawnEndOfTheConversation"/>.
/// </remarks>
sealed class ChatOfASession : UserControl
{
   /// <summary>The width of the column, as the app's narrow transcript width has it, in logical pixels.</summary>
   const double WidthOfTheColumn = 770;

   /// <summary>How near the top of what is drawn Magnus scrolls before more is drawn above it, in logical pixels.</summary>
   const double NearTheTop = 400;

   /// <summary>The share of the chat's height the input box grows to as Magnus writes more lines, as the Kotlin chat's did: past it, the box scrolls.</summary>
   const double MostOfTheHeightTheInputBoxTakes = 0.4;

   readonly TheHub _hub;
   readonly ItemsControl _column;
   readonly ScrollViewer _scroller;
   readonly TextBlock _notice = new() { IsVisible = false, Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 4) };

   readonly OpenedInTheChat _opened = new();

   readonly FollowingTheEnd _end;
   readonly DrawnEndOfTheConversation _drawn = new();
   CancellationTokenSource? _following;

   /// <summary>Whether drawing more above is under way: see <see cref="DrawMoreAboveOnceLaidOut"/>.</summary>
   bool _drawingMoreAbove;

   /// <summary>A chat that follows its conversation through <paramref name="hub"/>, and sends through it: the images pasted into its input box are saved in <paramref name="pastedImages"/>.</summary>
   public ChatOfASession(TheHub hub, PastedImages pastedImages)
   {
      _hub = hub;
      InputBox = new InputBoxOfTheChat(hub, pastedImages) { MaxWidth = WidthOfTheColumn + 32 };
      _column = new ItemsControl
      {
         ItemsSource = _drawn.Drawn,
         ItemTemplate = new FuncDataTemplate<ShownInTheChat?>((shown, _) => ViewOf(shown)),
         MaxWidth = WidthOfTheColumn,
         Margin = new Thickness(16, 12),
         HorizontalAlignment = HorizontalAlignment.Center
      };
      _scroller = new ScrollViewer { Content = _column, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Focusable = true };
      _end = new FollowingTheEnd(_scroller);
      KeepTheViewStillAsMoreIsDrawnAbove();
      _scroller.ScrollChanged += (_, changed) => { if(changed.OffsetDelta.Y < 0 && _scroller.Offset.Y <= NearTheTop) DrawMoreAboveOnceLaidOut(); };
      LookOfTheApp();
      SizeChanged += (_, size) => InputBox.MostHeight = size.NewSize.Height * MostOfTheHeightTheInputBoxTakes;
      Content = Rows(_notice, _scroller, InputBox);
   }

   /// <summary>Where Magnus writes to the Claude Code that runs in the tab.</summary>
   public InputBoxOfTheChat InputBox { get; }

   /// <summary>Gives the chat the keyboard, as the tab shows it: its input box, for Magnus to write.</summary>
   public void TakeTheKeyboard() => InputBox.TakeTheKeyboard();

   /// <summary>The session whose conversation the chat shows, along its line: null while there is none, as before Claude Code starts in a tab.</summary>
   public ClaudeSessionId? Session { get; private set; }

   /// <summary>What the chat shows now, in order, drawn or not.</summary>
   public IReadOnlyList<ShownInTheChat> Shown => _drawn.Shown;

   /// <summary>What it draws of it: the end, as far up as Magnus has scrolled.</summary>
   public IReadOnlyList<ShownInTheChat> Drawn => _drawn.Drawn;

   /// <summary>Whether it follows the end of the conversation as it goes on: until Magnus scrolls up, and again once he scrolls back down to the end.</summary>
   public bool FollowsTheEnd => _end.IsFollowing;

   /// <summary>What the chat tells above the conversation, as why it can't follow it: null while it tells nothing.</summary>
   public string? Notice => _notice.IsVisible ? _notice.Text : null;

   /// <summary>
   /// Shows the conversation of <paramref name="session"/>'s line, unless it shows it already, as after a <c>/clear</c>,
   /// which the hub's stream follows into the new session.
   /// </summary>
   public void Show(ClaudeSessionId? session)
   {
      if(session == Session) return;
      Session = session;
      FollowWhileShown();
   }

   protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
   {
      base.OnAttachedToVisualTree(e);
      FollowWhileShown();
   }

   protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
   {
      base.OnDetachedFromVisualTree(e);
      FollowWhileShown();
   }

   protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
   {
      base.OnPropertyChanged(change);
      if(change.Property == IsVisibleProperty) FollowWhileShown();
   }

   /// <summary>Follows the conversation of <see cref="Session"/> while the chat is shown, and stops following it otherwise.</summary>
   void FollowWhileShown()
   {
      _following?.Cancel();
      _following = null;
      if(Session is not {} session || !IsVisible || TopLevel.GetTopLevel(this) is null) return;

      var following = _following = new CancellationTokenSource();
      _ = Task.Run(async () =>
      {
         try
         {
            await foreach(var conversation in _hub.ConversationAsWritten(session, following.Token))
            {
               var shown = ShownInTheChat.AllOf(conversation);
               Dispatcher.UIThread.Post(() => { if(!following.IsCancellationRequested) Draw(shown); });
            }
         }
         catch(OperationCanceledException) when(following.IsCancellationRequested) {}
         catch(Exception failure)
         {
            Dispatcher.UIThread.Post(() => Tell($"The chat can't follow the conversation of {session}: {failure.Message}"));
         }
      });
   }

   /// <summary>Draws <paramref name="now"/>, what the chat is to show, in place of what it shows: see <see cref="DrawnEndOfTheConversation.Show"/>. Its first drawing is scrolled to the end.</summary>
   void Draw(IReadOnlyList<ShownInTheChat> now)
   {
      _notice.IsVisible = false;
      var isTheFirst = _drawn.Shown.Count == 0;
      _drawn.Show(now);
      if(isTheFirst) _end.ScrollToTheEnd();
   }

   /// <summary>
   /// Draws more above what is drawn, as Magnus scrolls up near its top, once the layout his scrolling started is done,
   /// and keeps what was drawn first where it was in view: the scroll viewer's anchor doesn't, at the very top.
   /// </summary>
   void DrawMoreAboveOnceLaidOut()
   {
      if(!_drawn.HasMoreAbove || _drawingMoreAbove) return;
      _drawingMoreAbove = true;
      Dispatcher.UIThread.Post(() =>
      {
         _drawingMoreAbove = false;
         if(_column.ContainerFromIndex(0) is not {} firstDrawn) return;
         var whereItWas = WhereInViewIs(firstDrawn);
         _drawn.DrawMoreAbove();
         _scroller.UpdateLayout();
         _scroller.Offset += new Vector(0, WhereInViewIs(firstDrawn) - whereItWas);
      });

      double WhereInViewIs(Control drawn) => drawn.TranslatePoint(default, _scroller)!.Value.Y;
   }

   /// <summary>
   /// Has what Magnus looks at stay where it is as what is drawn above it grows as its markdown is laid out: each thing
   /// drawn is a candidate for the scroll viewer's anchor, which it keeps in place.
   /// </summary>
   void KeepTheViewStillAsMoreIsDrawnAbove()
   {
      _column.ContainerPrepared += (_, prepared) => _scroller.RegisterAnchorCandidate(prepared.Container);
      _column.ContainerClearing += (_, clearing) => _scroller.UnregisterAnchorCandidate(clearing.Container);
   }

   void Tell(string notice)
   {
      _notice.Text = notice;
      _notice.IsVisible = true;
   }

   /// <summary>The view of <paramref name="shown"/>: none for null, which the list may ask for as it clears a view.</summary>
   Control ViewOf(ShownInTheChat? shown)
   {
      if(shown is null) return new Panel();
      var view = shown switch
      {
         ShownInTheChat.Prompt prompt => Bubble(Coloured(prompt.Text, ColoursOfTheChat.Text)),
         ShownInTheChat.SlashCommand command => Bubble(Coloured(command.Command, ColoursOfTheChat.SlashCommand), Coloured(command.Arguments, ColoursOfTheChat.Text)),
         ShownInTheChat.ClaudesText text => MarkdownOf(text.Markdown),
         ShownInTheChat.RunOfToolCalls run => ToolCallsInTheChat.Of(run, _opened),
         ShownInTheChat.Compaction compaction => Grey(compaction.Line),
         ShownInTheChat.Clearing => Grey(ShownInTheChat.Clearing.Line),
         ShownInTheChat.PromptFromElsewhere prompt => Grey(prompt.Line),
         _ => throw new ArgumentOutOfRangeException(nameof(shown), shown, "The chat has no view of it.")
      };
      view.Margin = new Thickness(0, 0, 0, shown is ShownInTheChat.Prompt or ShownInTheChat.SlashCommand ? 24 : 14);
      return view;
   }

   /// <summary>
   /// Has the chat look as the app's does: in the <see cref="ColoursOfTheChat"/> of Windows' theme, and the markdown of
   /// what Claude wrote in the sizes of the app's, through the resources LiveMarkdown's <c>Defaults.axaml</c> defines: a
   /// <c>#</c> heading about 1.25 times the text's size, <c>##</c> and <c>###</c> the text's own, bold.
   /// </summary>
   void LookOfTheApp()
   {
      Resources.MergedDictionaries.Add(ColoursOfTheChat.AsResources());
      Resources["FontSize3Xl"] = 17.5;
      Resources["FontSize2Xl"] = 14.0;
      Resources["FontSizeXl"] = 14.0;
   }

   static Control MarkdownOf(string markdown)
   {
      var text = new ObservableStringBuilder();
      text.Append(markdown);
      var renderer = ColoursOfTheChat.Text.AsTheForegroundOf(new MarkdownRenderer { MarkdownBuilder = text });
      renderer.AttachedToVisualTree += (_, _) => ColourTheCode();
      renderer.ActualThemeVariantChanged += (_, _) => ColourTheCode();
      return renderer;

      void ColourTheCode() => renderer.CodeBlockColorTheme = ColoursOfTheChat.OfCodeIn(renderer.ActualThemeVariant);
   }

   /// <summary>The chat's rows, top to bottom: <paramref name="notice"/> and <paramref name="inputBox"/> as tall as they are, <paramref name="conversation"/> taking the rest.</summary>
   static Grid Rows(Control notice, Control conversation, Control inputBox)
   {
      Grid.SetRow(notice, 0);
      Grid.SetRow(conversation, 1);
      Grid.SetRow(inputBox, 2);
      return new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Children = { notice, conversation, inputBox } };
   }
}
