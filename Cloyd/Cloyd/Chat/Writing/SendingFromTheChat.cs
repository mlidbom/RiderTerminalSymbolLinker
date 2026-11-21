using Cloyd.ClaudeCode;
using Cloyd.Hub;

namespace Cloyd.Chat.Writing;

/// <summary>
/// The messages Magnus sent from a chat's input box, given to Claude Code through the hub one after another, in the
/// order he sent them: giving one takes a second or two, as the hub types it, and the box is free for the next at once.
/// None interrupts Claude: while it works, Claude Code queues each, and takes it when it can, as it does those sent in
/// its own box.
/// </summary>
sealed class SendingFromTheChat(TheHub hub)
{
   readonly TheHub _hub = hub;

   /// <summary>The messages that wait for their turn, each with the Claude Code it is to be given to: the one that ran in the tab as Magnus sent it.</summary>
   readonly Queue<(MessageFromTheChat Message, ClaudeCodeProcessId ClaudeCode)> _waiting = new();

   bool _giving;

   /// <summary>
   /// Raised on the UI thread when Claude Code didn't get a message, with the messages it didn't get, in order: that one,
   /// and those that waited after it, which go back with it rather than overtake it. And why, for Magnus.
   /// </summary>
   public event Action<IReadOnlyList<MessageFromTheChat>, string>? NotGiven;

   /// <summary>
   /// The messages that wait for their turn to be given, in order: not the one being given, which the hub goes on typing
   /// whatever becomes of Cloyd.
   /// </summary>
   public IReadOnlyList<MessageFromTheChat> Waiting => [.. _waiting.Select(it => it.Message)];

   /// <summary>Whether a message is being given, or waits to be: false once Claude Code has them all.</summary>
   public bool IsGiving => _giving;

   /// <summary>Gives <paramref name="message"/> to the Claude Code that runs as <paramref name="claudeCode"/>, once those sent before it are given. Called on the UI thread.</summary>
   public void Give(MessageFromTheChat message, ClaudeCodeProcessId claudeCode)
   {
      _waiting.Enqueue((message, claudeCode));
      if(!_giving) _ = GiveThemInTurn();
   }

   /// <summary>
   /// Gives the waiting messages one after another: each that Claude Code didn't get, with those waiting after it, is told
   /// of by <see cref="NotGiven"/>, as this is where the hub's answer reaches Magnus.
   /// </summary>
   async Task GiveThemInTurn()
   {
      _giving = true;
      while(_waiting.TryDequeue(out var next))
      {
         try
         {
            await _hub.Send(next.Message, next.ClaudeCode);
         }
         catch(Exception failure)
         {
            List<MessageFromTheChat> notGiven = [next.Message, .. Waiting];
            _waiting.Clear();
            NotGiven?.Invoke(notGiven, failure.Message);
         }
      }

      _giving = false;
   }
}
