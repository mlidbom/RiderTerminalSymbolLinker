using Cloyd.ClaudeCode;
using Cloyd.Hub;

namespace Cloyd.Chat.Writing;

/// <summary>
/// The messages Magnus sent from a chat's input box, given to Claude Code through the hub one after another, in the
/// order he sent them: giving one takes a second or two, as the hub types it, and the box is free for the next at once.
/// None interrupts Claude: while it works, Claude Code queues each, and takes it when it can, as it does those sent in
/// its own box. Each is a <see cref="MessageOnItsWay"/> until the conversation has it.
/// </summary>
/// <remarks>Used on the UI thread.</remarks>
sealed class SendingFromTheChat(TheHub hub)
{
   readonly TheHub _hub = hub;

   /// <summary>The messages on their way, in the order Magnus sent them: those Claude Code has taken first, then those it is to be given.</summary>
   readonly List<MessageOnItsWay> _onTheirWay = [];

   /// <summary>The conversation, as the chat showed it last.</summary>
   IReadOnlyList<ShownInTheChat> _conversation = [];

   /// <summary>The message being given: null while none is.</summary>
   MessageOnItsWay? _beingGiven;

   /// <summary>
   /// Raised on the UI thread when Claude Code didn't get a message, with the messages it didn't get, in order: that one,
   /// and those that waited after it, which go back with it rather than overtake it. And why, for Magnus.
   /// </summary>
   public event Action<IReadOnlyList<MessageFromTheChat>, string>? NotGiven;

   /// <summary>Raised on the UI thread when <see cref="OnTheirWay"/> has changed.</summary>
   public event Action? OnTheirWayChanged;

   /// <summary>The messages sent that the conversation doesn't have yet, in the order Magnus sent them.</summary>
   public IReadOnlyList<MessageOnItsWay> OnTheirWay => [.. _onTheirWay];

   /// <summary>
   /// The messages that wait for their turn to be given, in order: not the one being given, which the hub goes on typing
   /// whatever becomes of Cloyd.
   /// </summary>
   public IReadOnlyList<MessageFromTheChat> Waiting => [.. NotGivenYet.Where(it => it != _beingGiven).Select(it => it.Message)];

   /// <summary>Whether a message is being given, or waits to be: false once Claude Code has them all.</summary>
   public bool IsGiving => _beingGiven is not null || NotGivenYet.Any();

   IEnumerable<MessageOnItsWay> NotGivenYet => _onTheirWay.Where(it => !it.IsGiven);

   /// <summary>Gives <paramref name="message"/> to the Claude Code that runs as <paramref name="claudeCode"/>, once those sent before it are given.</summary>
   public void Give(MessageFromTheChat message, ClaudeCodeProcessId claudeCode)
   {
      _onTheirWay.Add(new MessageOnItsWay(message, claudeCode, _conversation));
      OnTheirWayChanged?.Invoke();
      if(_beingGiven is null) _ = GiveThemInTurn();
   }

   /// <summary>The chat shows <paramref name="conversation"/> now: the messages on their way that it has are on their way no more.</summary>
   public void Seen(IReadOnlyList<ShownInTheChat> conversation)
   {
      _conversation = conversation;
      ForgetThoseTheConversationHas();
   }

   /// <summary>
   /// The Claude Code that runs in the tab is <paramref name="claudeCode"/> now: null while none runs. The messages given
   /// to another, which has exited, never reach the conversation: they are on their way no more.
   /// </summary>
   public void ClaudeCodeRuns(ClaudeCodeProcessId? claudeCode)
   {
      if(_onTheirWay.RemoveAll(it => it.IsGiven && it.ClaudeCode != claudeCode) > 0) OnTheirWayChanged?.Invoke();
   }

   /// <summary>
   /// Gives the messages not given yet one after another: each that Claude Code didn't get, with those waiting after it, is
   /// told of by <see cref="NotGiven"/>, as this is where the hub's answer reaches Magnus.
   /// </summary>
   async Task GiveThemInTurn()
   {
      while(NotGivenYet.FirstOrDefault() is {} next)
      {
         _beingGiven = next;
         try
         {
            next.WasGiven(queued: await _hub.Send(next.Message, next.ClaudeCode));
         }
         catch(Exception failure)
         {
            List<MessageFromTheChat> notGiven = [.. NotGivenYet.Select(it => it.Message)];
            _onTheirWay.RemoveAll(it => !it.IsGiven);
            NotGiven?.Invoke(notGiven, failure.Message);
         }

         _beingGiven = null;
         ForgetThoseTheConversationHas();
         OnTheirWayChanged?.Invoke();
      }
   }

   /// <summary>Forgets the messages on their way that the conversation has: given or being given, as the conversation may have one before the hub's answer comes.</summary>
   void ForgetThoseTheConversationHas()
   {
      if(_onTheirWay.RemoveAll(it => (it.IsGiven || it == _beingGiven) && it.IsIn(_conversation)) > 0) OnTheirWayChanged?.Invoke();
   }
}
