using Cloyd.Hub;
using Cloyd.Hub.RunningSessions;

namespace Cloyd.Chat.Writing;

/// <summary>The draft and ordered deliveries of a Claude Code chat. All editors of this session use
/// this object, so a failed delivery returns to the draft exactly once.</summary>
sealed class ClaudeCodeChatInput
{
   readonly TheHub _hub;
   readonly SendingFromTheChat _sending;
   public DraftOfAMessage Draft { get; } = new();
   public string? Notice { get; private set; }
   public event Action? Changed;

   public ClaudeCodeChatInput(TheHub hub)
   {
      _hub = hub;
      _sending = new SendingFromTheChat(hub);
      _sending.NotGiven += PutBackWhatClaudeCodeDidntGet;
      _sending.OnTheirWayChanged += () => Changed?.Invoke();
   }

   void Tell(string? notice)
   {
      Notice = notice;
      Changed?.Invoke();
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
         _sending.ClaudeCodeRuns(value?.Process);
      }
   }

   /// <summary>Whether messages sent are still being given to Claude Code, or wait to be.</summary>
   public bool IsGivingMessages => _sending.IsGiving;

   /// <summary>The messages sent that the conversation doesn't have yet, in the order Magnus sent them: the chat shows them over the box (<see cref="MessagesOnTheirWayOfTheChat"/>).</summary>
   public IReadOnlyList<MessageOnItsWay> MessagesOnTheirWay => _sending.OnTheirWay;

   /// <summary>The chat shows <paramref name="conversation"/> now: the messages on their way that it has are on their way no more.</summary>
   public void Seen(IReadOnlyList<ShownInTheChat> conversation) => _sending.Seen(conversation);

   /// <summary>
   /// What the box saves with its window: the messages that waited to be given to Claude Code, a blank line apart, then
   /// what Magnus was writing. All of it comes back into the box with the window, as the draft, to be sent again by him.
   /// </summary>
   public IReadOnlyList<PartOfAMessage> AsSaved => [.. TheParts(_sending.Waiting), .. Draft.Parts];

   /// <summary>
   /// Sends what the box holds, as Ctrl+Enter and the send button do: given to the Claude Code that runs in the tab, and
   /// the box empty for the next message at once. Nothing when it holds nothing; told over the box, and kept in it, while
   /// no Claude Code runs.
   /// </summary>
   public MessageFromTheChat? Send()
   {
      if(Draft.IsEmpty) return null;
      if(ClaudeCode is not {} claudeCode)
      {
         Tell("No Claude Code runs in this tab: start one in its terminal, and send again.");
         return null;
      }

      Tell(null);
      var message = Draft.TakeTheMessage();
      _sending.Give(message, claudeCode.Process);
      return message;
   }

   /// <summary>Interrupts Claude, as the Stop button asks: told over the box when the hub didn't, as when Claude was done already.</summary>
   public async Task Stop()
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
