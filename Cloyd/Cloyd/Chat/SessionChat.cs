using System.Collections.Immutable;
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
using Cloyd.Chat.Views;
using Cloyd.Chat.Writing;
using LiveMarkdown.Avalonia;

namespace Cloyd.Chat;

/// <summary>A session's searchable, scrolling conversation column and its composer. Each agent CLI supplies the
/// rows to show and the controls through which Magnus works with its session.</summary>
abstract class SessionChat : UserControl
{
   /// <summary>The width of the column, as the app's narrow transcript width has it, in logical pixels.</summary>
   protected const double WidthOfTheColumn = 770;

   /// <summary>The portion of the gap between rows that a turn-ending line omits to sit close to the reply.</summary>
   const double SpaceAboveAnOrdinaryRow = 10;

   /// <summary>How near the top of what is drawn Magnus scrolls before more is drawn above it, in logical pixels.</summary>
   const double NearTheTop = 400;

   /// <summary>The share of the chat's height the input box grows to as Magnus writes more lines, as the Kotlin chat's did: past it, the box scrolls.</summary>
   const double MostOfTheHeightTheInputBoxTakes = 0.4;

   readonly ItemsControl _column;
   readonly ScrollViewer _scroller;
   readonly TextBlock _notice = new() { IsVisible = false, Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 4) };

   readonly WhatTheViewFollows _followed;
   readonly DrawnEndOfTheConversation _drawn = new();
   ImmutableDictionary<string, string> _peerNames = ImmutableDictionary<string, string>.Empty;
   IReadOnlyList<ShownInTheChat> _recorded = [];
   Action? _requestArchivedPeerNames;

   /// <summary>Whether drawing more above is under way: see <see cref="DrawMoreAboveOnceLaidOut"/>.</summary>
   bool _drawingMoreAbove;

   /// <summary>What the search typed into the <see cref="FindBar"/> found: null while nothing is searched for.</summary>
   FoundInTheChat? _found;

   protected SessionChat()
   {
      _column = new ItemsControl
      {
         ItemsSource = _drawn.Drawn,
         ItemTemplate = new FuncDataTemplate<ShownInTheChat?>((shown, _) => ViewOf(shown)),
         MaxWidth = WidthOfTheColumn,
         Margin = new Thickness(16, 12 - SpaceAboveAnOrdinaryRow, 16, 12 + SpaceAboveAnOrdinaryRow),
         HorizontalAlignment = HorizontalAlignment.Stretch
      };
      _scroller = new ScrollViewer { Content = _column, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Focusable = true };
      _followed = new WhatTheViewFollows(_scroller);
      KeepTheViewStillAsMoreIsDrawnAbove();
      _scroller.ScrollChanged += (_, changed) => { if(changed.OffsetDelta.Y < 0 && _scroller.Offset.Y <= NearTheTop) DrawMoreAboveOnceLaidOut(); };
      SearchWithTheFindBar();
      LookOfTheApp();
   }

   protected void ArrangeRows(params Control[] under)
   {
      SizeChanged += (_, size) => InputBox.MostHeight = size.NewSize.Height * MostOfTheHeightTheInputBoxTakes;
      Content = Rows(_notice, new Panel { Children = { _scroller, FindBar } }, under);
   }

   public abstract ChatComposer InputBox { get; }
   public void TakeTheKeyboard() => InputBox.TakeTheKeyboard();

   /// <summary>The bar at the top right of the conversation that Magnus searches it with, as Ctrl+F opens it.</summary>
   public FindBarOfTheChat FindBar { get; } = new();

   /// <summary>
   /// The match of the search that the chat shows, painted apart from the others and scrolled to, once it is laid out:
   /// null while there is none.
   /// </summary>
   public TextHighlightMatch? MatchShown { get; private set; }

   /// <summary>What the chat shows now, in order, drawn or not.</summary>
   public IReadOnlyList<ShownInTheChat> Shown => _drawn.Shown;

   /// <summary>What it draws of it: the end, as far up as Magnus has scrolled.</summary>
   public IReadOnlyList<ShownInTheChat> Drawn => _drawn.Drawn;

   /// <summary>Whether it follows the end of the conversation as it goes on: until Magnus scrolls up, and again once he scrolls back down to the end.</summary>
   public bool FollowsTheEnd => _followed.IsFollowingTheEnd;

   /// <summary>What the chat tells above the conversation, as why it can't follow it: null while it tells nothing.</summary>
   public string? Notice => _notice.IsVisible ? _notice.Text : null;

   /// <summary>Whether the chat is shown: visible, in a window.</summary>
   protected bool IsShown => IsVisible && TopLevel.GetTopLevel(this) is not null;

   /// <summary>Draws the latest reading of the conversation and updates the active search.</summary>
   protected void ShowConversation(IReadOnlyList<ShownInTheChat> conversation)
   {
      _recorded = conversation;
      _drawn.Show(WithPeerNames());
      RefreshSearch();
   }

   void RefreshSearch()
   {
      if(_found is null) return;
      _found = _found.In(Shown);
      FindBar.Show(_found.Counted);
   }

   /// <summary>Resolves peers addressed by session ID, including rows that arrived before the hub listed their names.</summary>
   public void ShowPeerNames(ImmutableDictionary<string, string> names, Action requestArchivedPeerNames)
   {
      if(ReferenceEquals(_peerNames, names) && _requestArchivedPeerNames is not null) return;
      _requestArchivedPeerNames = requestArchivedPeerNames;
      _peerNames = names;
      _drawn.RefreshLabels(WithPeerNames());
      RefreshSearch();
   }

   IReadOnlyList<ShownInTheChat> WithPeerNames() => _requestArchivedPeerNames is null ? _recorded : _recorded.Select(WithPeerName).ToArray();

   ShownInTheChat WithPeerName(ShownInTheChat row)
   {
      if(row is not ShownInTheChat.PeerMessage peer || !Guid.TryParseExact(peer.Peer, "D", out _)) return row;
      if(_peerNames.TryGetValue(peer.Peer, out var name)) return peer with { Peer = name };
      _requestArchivedPeerNames!();
      return row;
   }

   protected void ScrollToTheEnd() => _followed.ScrollToTheEnd();

   /// <summary>Keeps the transcript at the row Magnus is opening or closing as its content changes height.</summary>
   public void KeepHeaderInPlace(Control header, Action toggle) => _followed.KeepHeaderInPlace(header, toggle);

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

   protected void Tell(string? notice)
   {
      _notice.Text = notice;
      _notice.IsVisible = notice is not null;
   }

   protected abstract Control DrawRow(ShownInTheChat shown);

   /// <summary>The view of <paramref name="shown"/>: none for null, which the list may ask for as it clears a view.</summary>
   Control ViewOf(ShownInTheChat? shown)
   {
      if(shown is null) return new Panel();
      var view = shown is ShownInTheChat.Prompt prompt ? LinesOfTheChat.PromptBubble(RenderMarkdown(prompt.Text)) : DrawRow(shown);
      var above = view is TurnEndLine ? 0 : SpaceAboveAnOrdinaryRow;
      var below = shown is ShownInTheChat.Prompt or ShownInTheChat.SlashCommand ? 14 : 4;
      view.Margin = new Thickness(0, above, 0, below);
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
   /// The matching Markdown block also renders after layout. Until then the view follows the row, and the
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
   /// prompts and replies in the sizes of the app's, through the resources LiveMarkdown's <c>Defaults.axaml</c> defines: a
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

   protected Control ReplyBubble(string markdown) => LinesOfTheChat.AgentBubble(RenderMarkdown(markdown), ColoursOfTheChat.ReplyBubble);

   MarkdownRenderer RenderMarkdown(string markdown)
   {
      var renderer = ChatMarkdown.Render(markdown);
      renderer.LinkClick += OpenChatLink;
      return renderer;
   }

   async void OpenChatLink(object? sender, LinkClickedEventArgs click)
   {
      if(click.HRef is not {} target) return;
      try
      {
         if(ChatLinks.Open(target, TopLevel.GetTopLevel(this)!.Launcher) is not {} opening) return;
         click.Handled = true;
         if(!await opening) Tell($"Couldn't open {target.OriginalString}.");
      }
      catch(Exception failure)
      {
         Tell($"Couldn't open {target.OriginalString}. {failure.Message}");
      }
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
