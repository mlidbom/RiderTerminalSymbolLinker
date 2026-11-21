using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Cloyd.Chat.Searching;
using Cloyd.Chat.Status;
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

   readonly WhatTheViewFollows _followed;
   readonly DrawnEndOfTheConversation _drawn = new();
   CancellationTokenSource? _following;
   CancellationTokenSource? _followingTheTurn;

   /// <summary>The Claude Code that runs in the tab, as the hub last told: null while none runs.</summary>
   RunningClaudeCode? _claudeCode;

   /// <summary>What the chat shows of the conversation, as the hub's stream of it sent it last.</summary>
   IReadOnlyList<ShownInTheChat> _conversation = [];

   /// <summary>
   /// The block Claude writes, or wrote last, in the turn under way, which the chat shows at the conversation's end until
   /// the conversation has it: null while there is none.
   /// </summary>
   BlockBeingWritten? _blockBeingWritten;

   /// <summary>Whether drawing more above is under way: see <see cref="DrawMoreAboveOnceLaidOut"/>.</summary>
   bool _drawingMoreAbove;

   /// <summary>What the search typed into the <see cref="FindBar"/> found: null while nothing is searched for.</summary>
   FoundInTheChat? _found;

   /// <summary>A chat that follows its conversation through <paramref name="hub"/>, and sends through it: the images pasted into its input box are saved in <paramref name="pastedImages"/>.</summary>
   public ChatOfASession(TheHub hub, PastedImages pastedImages)
   {
      _hub = hub;
      InputBox = new InputBoxOfTheChat(hub, pastedImages) { MaxWidth = WidthOfTheColumn + 32 };
      StatusLine = new StatusLineOfTheChat { MaxWidth = WidthOfTheColumn + 32 };
      WorkingLine = new WorkingLineOfTheChat { MaxWidth = WidthOfTheColumn + 32 };
      CallUnderWay = new CallUnderWayOfTheChat { MaxWidth = WidthOfTheColumn + 32 };
      MessagesOnTheirWay = new MessagesOnTheirWayOfTheChat { MaxWidth = WidthOfTheColumn + 32 };
      InputBox.MessagesOnTheirWayChanged += () => MessagesOnTheirWay.Show(InputBox.MessagesOnTheirWay);
      WaitingForMagnusInTheTerminal = new WaitingForMagnusInTheTerminal { MaxWidth = WidthOfTheColumn + 32 };
      _column = new ItemsControl
      {
         ItemsSource = _drawn.Drawn,
         ItemTemplate = new FuncDataTemplate<ShownInTheChat?>((shown, _) => ViewOf(shown)),
         MaxWidth = WidthOfTheColumn,
         Margin = new Thickness(16, 12),
         HorizontalAlignment = HorizontalAlignment.Stretch
      };
      _scroller = new ScrollViewer { Content = _column, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Focusable = true };
      _followed = new WhatTheViewFollows(_scroller);
      KeepTheViewStillAsMoreIsDrawnAbove();
      _scroller.ScrollChanged += (_, changed) => { if(changed.OffsetDelta.Y < 0 && _scroller.Offset.Y <= NearTheTop) DrawMoreAboveOnceLaidOut(); };
      SearchWithTheFindBar();
      LookOfTheApp();
      SizeChanged += (_, size) => InputBox.MostHeight = size.NewSize.Height * MostOfTheHeightTheInputBoxTakes;
      Content = Rows(_notice, new Panel { Children = { _scroller, FindBar } }, WaitingForMagnusInTheTerminal, CallUnderWay, MessagesOnTheirWay, WorkingLine, InputBox, StatusLine);
   }

   /// <summary>The bar at the top right of the conversation that Magnus searches it with, as Ctrl+F opens it.</summary>
   public FindBarOfTheChat FindBar { get; } = new();

   /// <summary>
   /// The match of the search that the chat shows, painted apart from the others and scrolled to, once it is laid out:
   /// null while there is none.
   /// </summary>
   public TextHighlightMatch? MatchShown { get; private set; }

   /// <summary>Where Magnus writes to the Claude Code that runs in the tab.</summary>
   public InputBoxOfTheChat InputBox { get; }

   /// <summary>The lines under the input box: Claude Code's own status line, its permission mode, and the background work that runs.</summary>
   public StatusLineOfTheChat StatusLine { get; }

   /// <summary>The turn's line over the input box while Claude works: at what, and how long it has.</summary>
   public WorkingLineOfTheChat WorkingLine { get; }

   /// <summary>The tool call under way while Claude works, at the top of what shows over the input box.</summary>
   public CallUnderWayOfTheChat CallUnderWay { get; }

   /// <summary>The messages sent that the conversation doesn't have yet, between the call under way and the turn's line.</summary>
   public MessagesOnTheirWayOfTheChat MessagesOnTheirWay { get; }

   /// <summary>The box between the conversation and the input box while Claude waits for Magnus in the terminal.</summary>
   public WaitingForMagnusInTheTerminal WaitingForMagnusInTheTerminal { get; }

   /// <summary>
   /// The Claude Code that runs in the tab's host, as the hub last told: what the input box sends to, and the lines and the
   /// box around it tell of; whose turn under way the chat shows as Claude writes it. Null while none runs, as before it
   /// starts, or after it exits to the shell.
   /// </summary>
   public RunningClaudeCode? ClaudeCode
   {
      set
      {
         var anotherRuns = value?.Process != _claudeCode?.Process;
         var otherTasksRun = value?.BackgroundWork?.TasksCounted != _claudeCode?.BackgroundWork?.TasksCounted;
         _claudeCode = value;
         if(otherTasksRun) DrawWhatIsShown();
         InputBox.ClaudeCode = value;
         StatusLine.Show(value);
         WorkingLine.Show(value);
         CallUnderWay.Show(value);
         WaitingForMagnusInTheTerminal.Show(value);
         if(anotherRuns) FollowTheTurnWhileShown();
      }
   }

   /// <summary>Gives the chat the keyboard, as the tab shows it: its input box, for Magnus to write.</summary>
   public void TakeTheKeyboard() => InputBox.TakeTheKeyboard();

   /// <summary>The session whose conversation the chat shows, along its line: null while there is none, as before Claude Code starts in a tab.</summary>
   public ClaudeCodeSessionId? Session { get; private set; }

   /// <summary>What the chat shows now, in order, drawn or not.</summary>
   public IReadOnlyList<ShownInTheChat> Shown => _drawn.Shown;

   /// <summary>What it draws of it: the end, as far up as Magnus has scrolled.</summary>
   public IReadOnlyList<ShownInTheChat> Drawn => _drawn.Drawn;

   /// <summary>Whether it follows the end of the conversation as it goes on: until Magnus scrolls up, and again once he scrolls back down to the end.</summary>
   public bool FollowsTheEnd => _followed.IsFollowingTheEnd;

   /// <summary>What the chat tells above the conversation, as why it can't follow it: null while it tells nothing.</summary>
   public string? Notice => _notice.IsVisible ? _notice.Text : null;

   /// <summary>
   /// Shows the conversation of <paramref name="session"/>'s line, unless it shows it already, as after a <c>/clear</c>,
   /// which the hub's stream follows into the new session.
   /// </summary>
   public void Show(ClaudeCodeSessionId? session)
   {
      if(session == Session) return;
      Session = session;
      FollowWhileShown();
   }

   protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
   {
      base.OnAttachedToVisualTree(e);
      FollowWhileShown();
      FollowTheTurnWhileShown();
   }

   protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
   {
      base.OnDetachedFromVisualTree(e);
      FollowWhileShown();
      FollowTheTurnWhileShown();
   }

   protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
   {
      base.OnPropertyChanged(change);
      if(change.Property != IsVisibleProperty) return;
      FollowWhileShown();
      FollowTheTurnWhileShown();
   }

   /// <summary>Whether the chat is shown: visible, in a window.</summary>
   bool IsShown => IsVisible && TopLevel.GetTopLevel(this) is not null;

   /// <summary>Follows the conversation of <see cref="Session"/> while the chat is shown, and stops following it otherwise.</summary>
   void FollowWhileShown()
   {
      _following?.Cancel();
      _following = null;
      if(Session is not {} session || !IsShown) return;

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

   /// <summary>
   /// Follows the turn of the Claude Code that runs in the tab while the chat is shown: the block Claude writes, which the
   /// chat shows at the conversation's end until the conversation has it. Stops following it otherwise.
   /// </summary>
   void FollowTheTurnWhileShown()
   {
      _followingTheTurn?.Cancel();
      _followingTheTurn = null;
      Show(block: null);
      if(_claudeCode?.Process is not {} claudeCode || !IsShown) return;

      var following = _followingTheTurn = new CancellationTokenSource();
      _ = Task.Run(async () =>
      {
         try
         {
            await foreach(var block in _hub.BlockBeingWrittenIn(claudeCode, following.Token))
               Dispatcher.UIThread.Post(() => { if(!following.IsCancellationRequested) Show(block); });
         }
         catch(OperationCanceledException) when(following.IsCancellationRequested) {}
         catch(RefusedByTheHub) {} // The Claude Code has exited: the hub's next list of the running sessions tells the tab so.
         catch(Exception failure)
         {
            Dispatcher.UIThread.Post(() => Tell($"The chat can't follow what Claude writes as it comes: {failure.Message}"));
         }
      });
   }

   /// <summary>
   /// Draws <paramref name="conversation"/>, what the chat shows of the conversation as the hub sent it last: the block
   /// Claude wrote last, once ended, is in it now, and no longer shown after it. Tells the input box and the working line
   /// what it has. Its first drawing is scrolled to the end.
   /// </summary>
   void Draw(IReadOnlyList<ShownInTheChat> conversation)
   {
      _notice.IsVisible = false;
      var isTheFirst = _conversation.Count == 0;
      _conversation = conversation;
      if(_blockBeingWritten is { HasEnded: true }) _blockBeingWritten = null;
      InputBox.Seen(conversation);
      WorkingLine.Show(conversation);
      CallUnderWay.Show(conversation);
      DrawWhatIsShown();
      if(isTheFirst) _followed.ScrollToTheEnd();
   }

   /// <summary>Shows <paramref name="block"/>, the block Claude writes, or wrote last, at the conversation's end, until the conversation has it.</summary>
   void Show(BlockBeingWritten? block)
   {
      if(block == _blockBeingWritten) return;
      _blockBeingWritten = block;
      DrawWhatIsShown();
   }

   /// <summary>
   /// Draws the conversation, its last turn's end telling what runs in the background, and after it the text Claude
   /// writes, in place of what the chat draws: see <see cref="DrawnEndOfTheConversation.Show"/>.
   /// </summary>
   void DrawWhatIsShown()
   {
      var conversation = WithWhatTheLastTurnLeftRunning(_conversation);
      _drawn.Show(_blockBeingWritten is { IsText: true } block ? [.. conversation, new ShownInTheChat.ClaudesText(block.Id, block.Text)] : conversation);
      if(_found is null) return;
      _found = _found.In(Shown);
      FindBar.Show(_found.Counted);
   }

   /// <summary>
   /// <paramref name="conversation"/>, its end, when a turn's, telling what that turn left running in the background: the
   /// background work as of the last turn's end is all the hub tells of.
   /// </summary>
   IReadOnlyList<ShownInTheChat> WithWhatTheLastTurnLeftRunning(IReadOnlyList<ShownInTheChat> conversation) =>
      conversation is [.., ShownInTheChat.EndOfATurn last] ? [.. conversation.SkipLast(1), last.TheLastWith(_claudeCode?.BackgroundWork)] : conversation;

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
         ShownInTheChat.EndOfATurn end => Grey(end.Line),
         _ => throw new ArgumentOutOfRangeException(nameof(shown), shown, "The chat has no view of it.")
      };
      view.Margin = new Thickness(0, 0, 0, shown is ShownInTheChat.Prompt or ShownInTheChat.SlashCommand ? 24 : 14);
      if(_found is {} found) MatchesPainted.In(view, found.Pattern);
      return view;
   }

   /// <summary>
   /// Has Magnus search the conversation with the <see cref="FindBar"/>, as in a browser: Ctrl+F opens it, F3 shows the next
   /// match and Shift+F3 the one before, wherever the keyboard is in the chat; what he types into it is searched for as he
   /// types.
   /// </summary>
   void SearchWithTheFindBar()
   {
      AddHandler(KeyDownEvent, SearchOnItsKeys, RoutingStrategies.Tunnel);
      FindBar.TextChanged += Find;
      FindBar.NextAsked += () => Show(_found?.Next());
      FindBar.PreviousAsked += () => Show(_found?.Previous());
      FindBar.Closed += () =>
      {
         Find("");
         TakeTheKeyboard();
      };
   }

   void SearchOnItsKeys(object? sender, KeyEventArgs key)
   {
      switch(key)
      {
         case { Key: Key.F, KeyModifiers: KeyModifiers.Control }:
            FindBar.Open();
            break;
         case { Key: Key.F3, KeyModifiers: KeyModifiers.None or KeyModifiers.Shift } when !FindBar.IsOpen:
            FindBar.Open();
            break;
         case { Key: Key.F3, KeyModifiers: KeyModifiers.None }:
            Show(_found?.Next());
            break;
         case { Key: Key.F3, KeyModifiers: KeyModifiers.Shift }:
            Show(_found?.Previous());
            break;
         default:
            return;
      }

      key.Handled = true;
   }

   /// <summary>
   /// Searches the conversation for <paramref name="text"/>, painting each match drawn, and shows the first from the top of
   /// the view on (<see cref="FoundInTheChat.Of"/>): no more searching when it is empty.
   /// </summary>
   void Find(string text)
   {
      _found = text.Length > 0 ? FoundInTheChat.Of(text, Shown, FirstInView()) : null;
      foreach(var view in _column.GetRealizedContainers().OfType<ContentPresenter>().Select(it => it.Child).OfType<Control>())
         MatchesPainted.In(view, _found?.Pattern);
      Show(_found);
   }

   /// <summary>Where in <see cref="Shown"/> the first thing in view is: the first drawn whose foot is below the view's top.</summary>
   int FirstInView()
   {
      for(var at = 0; at < _drawn.Drawn.Count; at++)
         if(_column.ContainerFromIndex(at) is {} drawn && drawn.TranslatePoint(new Point(0, drawn.Bounds.Height), _scroller) is { Y: > 0 })
            return _drawn.FirstDrawn + at;
      return _drawn.FirstDrawn;
   }

   /// <summary>
   /// Shows the match <paramref name="found"/> shows, which the search is from then on: draws the conversation as far up as
   /// the match is, and once it is laid out, paints it apart from the others and scrolls to it.
   /// </summary>
   void Show(FoundInTheChat? found)
   {
      _found = found;
      FindBar.Show(found?.Counted);
      if(MatchShown is {} shownBefore) MatchesPainted.NoLongerShown(shownBefore);
      MatchShown = null;
      _followed.StopFollowingInPlaceOfTheEnd();
      if(found?.Shown is not {} match) return;

      _drawn.DrawAsFarUpAs(match.Shown);
      ShowOnceLaidOut(found, match, triesLeft: 40);
   }

   /// <summary>
   /// Paints <paramref name="match"/> as the one shown and has the view follow it once the chat is laid out, unless another
   /// search or match is shown by then: what was drawn above it grows as its markdown is rendered, after it is laid out.
   /// Claude's markdown that the match is in is too: until it has been, the view follows the view the match is in, and the
   /// chat tries again a little later, as many as <paramref name="triesLeft"/> more times.
   /// </summary>
   void ShowOnceLaidOut(FoundInTheChat found, MatchInTheChat match, int triesLeft) =>
      Dispatcher.UIThread.Post(() =>
      {
         if(found != _found || _column.ContainerFromIndex(match.Shown - _drawn.FirstDrawn) is not ContentPresenter { Child: {} view }) return;

         if(MatchesPainted.AsTheOneShown(view, found.Pattern, match.Occurrence) is {} shown)
         {
            MatchShown = shown;
            _followed.FollowInPlaceOfTheEnd(shown.Block, MatchesPainted.BoundsOf(shown));
            return;
         }

         _followed.FollowInPlaceOfTheEnd(view, new Rect(view.Bounds.Size));
         if(triesLeft > 0) DispatcherTimer.RunOnce(() => ShowOnceLaidOut(found, match, triesLeft - 1), TimeSpan.FromMilliseconds(50));
      }, DispatcherPriority.Loaded);

   /// <summary>
   /// Has the chat look as the app's does: in the <see cref="ColoursOfTheChat"/> of Windows' theme, and the markdown of
   /// what Claude wrote in the sizes of the app's, through the resources LiveMarkdown's <c>Defaults.axaml</c> defines: a
   /// <c>#</c> heading about 1.25 times the text's size, <c>##</c> and <c>###</c> the text's own, bold.
   /// </summary>
   void LookOfTheApp()
   {
      Resources.MergedDictionaries.Add(ColoursOfTheChat.AsResources());
      MarkdownTextBlock.SetHighlightStyles(this, MatchesPainted.StylesIn(ActualThemeVariant));
      ActualThemeVariantChanged += (_, _) => MarkdownTextBlock.SetHighlightStyles(this, MatchesPainted.StylesIn(ActualThemeVariant));
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

   /// <summary>The chat's rows, top to bottom: <paramref name="notice"/>, <paramref name="conversation"/> taking what the others leave, and those <paramref name="under"/> it, each as tall as it is.</summary>
   static Grid Rows(Control notice, Control conversation, params Control[] under)
   {
      var rows = new Grid { RowDefinitions = new RowDefinitions("Auto,*" + string.Concat(under.Select(_ => ",Auto"))) };
      foreach(var (row, at) in new[] { notice, conversation }.Concat(under).Select((row, at) => (row, at)))
      {
         Grid.SetRow(row, at);
         rows.Children.Add(row);
      }

      return rows;
   }
}
