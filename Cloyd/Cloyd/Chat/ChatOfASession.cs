using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Cloyd.Chat.Views;
using Cloyd.ClaudeCode;
using Cloyd.Hub;
using LiveMarkdown.Avalonia;
using static Cloyd.Chat.Views.LinesOfTheChat;

namespace Cloyd.Chat;

/// <summary>
/// The chat of a session: its conversation, as the hub's stream of it tells, drawn as the Claude desktop app's Code tab
/// draws one (<c>dev_docs/cloyd-chat-specification.md</c>) in a column down the middle: Magnus's prompts in bubbles at the
/// right, what Claude wrote across the column, its tool calls folded into grey lines.
/// </summary>
/// <remarks>
/// It follows the conversation only while it is shown, as a tab's selected view: the hub holds every message of a
/// conversation in memory while a stream follows it. Shown again, it reads the conversation anew, and draws only what
/// changed meanwhile.<br/>
/// A long conversation, such as one of Magnus's of 15,000 messages, is drawn only where it is scrolled to: a virtualizing
/// list.
/// </remarks>
sealed class ChatOfASession : UserControl
{
   /// <summary>The width of the column, as the app's narrow transcript width has it, in logical pixels.</summary>
   const double WidthOfTheColumn = 770;

   readonly TheHub _hub;
   readonly ItemsControl _column;
   readonly ScrollViewer _scroller;
   readonly TextBlock _notice = new() { IsVisible = false, Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 4) };

   /// <summary>The runs of tool calls Magnus has opened, by their <see cref="ShownInTheChat.RunOfToolCalls.Id"/>: a run stays open as calls are added to it.</summary>
   readonly HashSet<string> _openedRuns = [];

   readonly FollowingTheEnd _end;
   ObservableCollection<ShownInTheChat> _shown = [];
   CancellationTokenSource? _following;

   public ChatOfASession(TheHub hub)
   {
      _hub = hub;
      _column = new ItemsControl
      {
         ItemsSource = _shown,
         ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel()),
         ItemTemplate = new FuncDataTemplate<ShownInTheChat?>((shown, _) => ViewOf(shown)),
         MaxWidth = WidthOfTheColumn,
         Margin = new Thickness(16, 12),
         HorizontalAlignment = HorizontalAlignment.Center
      };
      _scroller = new ScrollViewer { Content = _column, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
      _end = new FollowingTheEnd(_scroller);
      LookOfClaudesMarkdown();
      Content = new DockPanel { Children = { Docked(_notice), _scroller } };
   }

   /// <summary>The session whose conversation the chat shows, along its line: null while there is none, as before Claude Code starts in a tab.</summary>
   public ClaudeSessionId? Session { get; private set; }

   /// <summary>What the chat shows now, in order.</summary>
   public IReadOnlyList<ShownInTheChat> Shown => _shown;

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

   /// <summary>
   /// Draws <paramref name="now"/>, what the chat is to show, in place of what it shows: only what differs, from the first
   /// thing that does on, as what the conversation adds is at its end.
   /// </summary>
   void Draw(IReadOnlyList<ShownInTheChat> now)
   {
      _notice.IsVisible = false;
      if(_shown.Count == 0)
      {
         _column.ItemsSource = _shown = new ObservableCollection<ShownInTheChat>(now);
         _end.ScrollToTheEnd();
         return;
      }

      var same = 0;
      while(same < _shown.Count && same < now.Count && _shown[same].Equals(now[same])) same++;
      while(_shown.Count > same) _shown.RemoveAt(_shown.Count - 1);
      foreach(var added in now.Skip(same)) _shown.Add(added);
   }

   void Tell(string notice)
   {
      _notice.Text = notice;
      _notice.IsVisible = true;
   }

   /// <summary>The view of <paramref name="shown"/>: none for null, which the virtualizing list asks for as it recycles a view.</summary>
   Control ViewOf(ShownInTheChat? shown)
   {
      if(shown is null) return new Panel();
      var view = shown switch
      {
         ShownInTheChat.Prompt prompt => Bubble(Coloured(prompt.Text, ColoursOfTheChat.Text)),
         ShownInTheChat.SlashCommand command => Bubble(Coloured(command.Command, ColoursOfTheChat.SlashCommand), Coloured(command.Arguments, ColoursOfTheChat.Text)),
         ShownInTheChat.ClaudesText text => MarkdownOf(text.Markdown),
         ShownInTheChat.RunOfToolCalls run => ToolCallsInTheChat.Of(run, _openedRuns.Contains(run.Id), isOpen => { if(isOpen) _openedRuns.Add(run.Id); else _openedRuns.Remove(run.Id); }),
         ShownInTheChat.Compaction compaction => Grey(compaction.Line),
         ShownInTheChat.Clearing => Grey(ShownInTheChat.Clearing.Line),
         ShownInTheChat.PromptFromElsewhere prompt => Grey(prompt.Line),
         _ => throw new ArgumentOutOfRangeException(nameof(shown), shown, "The chat has no view of it.")
      };
      view.Margin = new Thickness(0, 0, 0, shown is ShownInTheChat.Prompt or ShownInTheChat.SlashCommand ? 24 : 14);
      return view;
   }

   /// <summary>
   /// Has the markdown of what Claude wrote look as the app's does, through the resources LiveMarkdown's
   /// <c>Defaults.axaml</c> defines: a <c>#</c> heading about 1.25 times the text's size, <c>##</c> and <c>###</c> the
   /// text's own, bold; inline code in the text's colour.
   /// </summary>
   void LookOfClaudesMarkdown()
   {
      Resources["FontSize3Xl"] = 17.5;
      Resources["FontSize2Xl"] = 14.0;
      Resources["FontSizeXl"] = 14.0;
      Resources["CodeInlineColor"] = ((ISolidColorBrush)ColoursOfTheChat.Text).Color;
   }

   static Control MarkdownOf(string markdown)
   {
      var text = new ObservableStringBuilder();
      text.Append(markdown);
      var renderer = new MarkdownRenderer { MarkdownBuilder = text };
      TextElement.SetForeground(renderer, ColoursOfTheChat.Text);
      return renderer;
   }

   static Control Docked(Control control)
   {
      DockPanel.SetDock(control, Dock.Top);
      return control;
   }
}
