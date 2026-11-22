using Cloyd.Hub;
using Cloyd.Hub.RunningSessions;
using Cloyd.Pasting;

namespace Cloyd.Chat.Writing;

/// <summary>
/// The chat's input box, where Magnus writes a message to the Claude Code that runs in the tab, as the Claude desktop
/// app's composer is, and sends it: through the hub, which types it into Claude Code's own box as he would.
/// </summary>
/// <remarks>
/// Its keys, as Magnus chose them (2026-11-21; <c>dev_docs/cloyd-as-a-dotnet-application.md</c>, "The input box,
/// Magnus's calls"):
/// <list type="bullet">
/// <item>Ctrl+Enter sends; Enter starts a new line, as in every editor.</item>
/// <item>Nothing interrupts Claude by accident. Escape does nothing to it, nor does sending: a message sent while Claude
/// works waits, and Claude Code takes it when it can. Only the Stop button, shown while Claude works, interrupts.</item>
/// <item>Ctrl+V pastes text as pasted, apart from what he types, and an image with no text on the clipboard as an image,
/// shown as a thumbnail over the box.</item>
/// </list>
/// </remarks>
sealed class InputBoxOfTheChat : ChatComposer
{
   /// <summary>The tip of the button that stops Claude.</summary>
   public const string TipOfTheStopButton = "Stop Claude";

   readonly TheHub _hub;
   readonly SendingFromTheChat _sending;

   /// <summary>A box that sends through <paramref name="hub"/>, and saves the images pasted into it in <paramref name="pastedImages"/>.</summary>
   public InputBoxOfTheChat(TheHub hub, PastedImages pastedImages) : base(pastedImages, "Reply to Claude.", TipOfTheStopButton)
   {
      _hub = hub;
      _sending = new SendingFromTheChat(hub);
      _sending.NotGiven += PutBackWhatClaudeCodeDidntGet;
      _sending.OnTheirWayChanged += () => MessagesOnTheirWayChanged?.Invoke();
   }

   /// <summary>
   /// The Claude Code the box sends to: the one that runs in the tab's host, as the hub last told. Null while none runs, as
   /// before it starts, or after it exits to the shell.
   /// </summary>
   public RunningClaudeCode? ClaudeCode
   {
      get;
      set
      {
         field = value;
         ShowsTheStopButton = value?.Status.IsBusy == true;
         _sending.ClaudeCodeRuns(value?.Process);
      }
   }

   /// <summary>Whether messages sent are still being given to Claude Code, or wait to be.</summary>
   public bool IsGivingMessages => _sending.IsGiving;

   /// <summary>The messages sent that the conversation doesn't have yet, in the order Magnus sent them: the chat shows them over the box (<see cref="MessagesOnTheirWayOfTheChat"/>).</summary>
   public IReadOnlyList<MessageOnItsWay> MessagesOnTheirWay => _sending.OnTheirWay;

   /// <summary>Raised on the UI thread when <see cref="MessagesOnTheirWay"/> has changed.</summary>
   public event Action? MessagesOnTheirWayChanged;

   /// <summary>The chat shows <paramref name="conversation"/> now: the messages on their way that it has are on their way no more.</summary>
   public void Seen(IReadOnlyList<ShownInTheChat> conversation) => _sending.Seen(conversation);

   /// <summary>
   /// What the box saves with its window: the messages that waited to be given to Claude Code, a blank line apart, then
   /// what Magnus was writing. All of it comes back into the box with the window, as the draft, to be sent again by him.
   /// </summary>
   public override IReadOnlyList<PartOfAMessage> AsSaved => [.. TheParts(_sending.Waiting), .. Draft.Parts];

   /// <summary>Raised when Magnus has sent a slash command, such as <c>/compact</c>, which shows what it does in Claude Code's own UI.</summary>
   public event Action? SentASlashCommand;

   /// <summary>
   /// Sends what the box holds, as Ctrl+Enter and the send button do: given to the Claude Code that runs in the tab, and
   /// the box empty for the next message at once. Nothing when it holds nothing; told over the box, and kept in it, while
   /// no Claude Code runs.
   /// </summary>
   public override void Send()
   {
      if(Draft.IsEmpty) return;
      if(ClaudeCode is not {} claudeCode)
      {
         Tell("No Claude Code runs in this tab: start one in its terminal, and send again.");
         return;
      }

      Tell(null);
      var message = Draft.TakeTheMessage();
      _sending.Give(message, claudeCode.Process);
      if(message.IsASlashCommand) SentASlashCommand?.Invoke();
   }

   /// <summary>Interrupts Claude, as the Stop button asks: told over the box when the hub didn't, as when Claude was done already.</summary>
   protected override async Task Stop()
   {
      if(ClaudeCode is not {} claudeCode) return;
      try
      {
         await _hub.Interrupt(claudeCode.Process);
      }
      catch(Exception failure)
      {
         Tell($"Claude wasn't stopped: {failure.Message}");
      }
   }

   /// <summary>
   /// Puts back into the box the messages Claude Code didn't get, <paramref name="notGiven"/>, ahead of what Magnus wrote
   /// since, and tells <paramref name="why"/> over it, for him to send them again once he has seen to it.
   /// </summary>
   void PutBackWhatClaudeCodeDidntGet(IReadOnlyList<MessageFromTheChat> notGiven, string why)
   {
      Draft.PutBackAhead(TheParts(notGiven));
      Tell($"Claude Code didn't get the message, which is back in the box: {why}");
   }

   /// <summary>The parts of <paramref name="messages"/>, in order, a blank line between one and the next.</summary>
   static IReadOnlyList<PartOfAMessage> TheParts(IReadOnlyList<MessageFromTheChat> messages) =>
      [.. messages.SelectMany((message, at) => at == 0 ? message.Parts : [new PartOfAMessage.TypedText("\n\n"), .. message.Parts])];

}
