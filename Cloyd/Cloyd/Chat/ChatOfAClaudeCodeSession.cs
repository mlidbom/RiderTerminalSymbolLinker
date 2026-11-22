using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Cloyd.Chat.Status;
using Cloyd.Chat.Views;
using Cloyd.Chat.Writing;
using Cloyd.ClaudeCode;
using Cloyd.Hub;
using Cloyd.Hub.RunningSessions;
using Cloyd.Pasting;
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
sealed class ChatOfAClaudeCodeSession : SessionChat
{
   readonly TheHub _hub;
   readonly OpenedInTheChat _opened = new();

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

   /// <summary>A chat that follows its conversation through <paramref name="hub"/>, and sends through it: the images pasted into its input box are saved in <paramref name="pastedImages"/>.</summary>
   public ChatOfAClaudeCodeSession(TheHub hub, PastedImages pastedImages)
   {
      _hub = hub;
      InputBox = new InputBoxOfTheChat(hub, pastedImages) { MaxWidth = WidthOfTheColumn + 32 };
      StatusLine = new StatusLineOfTheChat { MaxWidth = WidthOfTheColumn + 32 };
      WorkingLine = new WorkingLineOfTheChat { MaxWidth = WidthOfTheColumn + 32 };
      CallUnderWay = new CallUnderWayOfTheChat { MaxWidth = WidthOfTheColumn + 32 };
      MessagesOnTheirWay = new MessagesOnTheirWayOfTheChat { MaxWidth = WidthOfTheColumn + 32 };
      InputBox.MessagesOnTheirWayChanged += () => MessagesOnTheirWay.Show(InputBox.MessagesOnTheirWay);
      WaitingForMagnusInTheTerminal = new WaitingForMagnusInTheTerminal { MaxWidth = WidthOfTheColumn + 32 };
      ArrangeRows(WaitingForMagnusInTheTerminal, CallUnderWay, MessagesOnTheirWay, WorkingLine, InputBox, StatusLine);
   }

   /// <summary>Where Magnus writes to the Claude Code that runs in the tab.</summary>
   public override InputBoxOfTheChat InputBox { get; }

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

   /// <summary>The session whose conversation the chat shows, along its line: null while there is none, as before Claude Code starts in a tab.</summary>
   public ClaudeCodeSessionId? Session { get; private set; }

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
      Tell(null);
      var isTheFirst = _conversation.Count == 0;
      _conversation = conversation;
      if(_blockBeingWritten is { HasEnded: true }) _blockBeingWritten = null;
      InputBox.Seen(conversation);
      WorkingLine.Show(conversation);
      CallUnderWay.Show(conversation);
      DrawWhatIsShown();
      if(isTheFirst) ScrollToTheEnd();
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
      ShowConversation(_blockBeingWritten is { IsText: true } block ? [.. conversation, new ShownInTheChat.ClaudesText(block.Id, block.Text)] : conversation);
   }

   /// <summary>
   /// <paramref name="conversation"/>, its end, when a turn's, telling what that turn left running in the background: the
   /// background work as of the last turn's end is all the hub tells of.
   /// </summary>
   IReadOnlyList<ShownInTheChat> WithWhatTheLastTurnLeftRunning(IReadOnlyList<ShownInTheChat> conversation) =>
      conversation is [.., ShownInTheChat.EndOfATurn last] ? [.. conversation.SkipLast(1), last.TheLastWith(_claudeCode?.BackgroundWork)] : conversation;

   protected override Control DrawRow(ShownInTheChat shown) => shown switch
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
}
