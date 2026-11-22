using Avalonia.Threading;
using Cloyd.Chat.Writing;
using Cloyd.Chat.Status;
using Cloyd.ClaudeCode;
using Cloyd.Hub;

namespace Cloyd.Chat;

/// <summary>The recorded conversation seen by every visible Claude Code chat of a host. One ordered stream
/// acknowledges <see cref="ClaudeCodeChatInput"/> deliveries before each view renders the same snapshot.</summary>
sealed class ClaudeCodeConversation(TheHub hub, ClaudeCodeChatInput input, ClaudeCodeTurnStatus status)
{
   Action<IReadOnlyList<ShownInTheChat>>? _draw;
   Action<string>? _tell;
   ClaudeCodeSessionId? _session;
   CancellationTokenSource? _following;
   IReadOnlyList<ShownInTheChat>? _latest;

   public void Follow(ClaudeCodeSessionId session, Action<IReadOnlyList<ShownInTheChat>> draw, Action<string> tell)
   {
      _draw += draw;
      _tell += tell;
      if(_session == session && _following is not null)
      {
         if(_latest is {} latest) draw(latest);
         return;
      }
      Stop();
      _session = session;
      var following = _following = new CancellationTokenSource();
      _ = Read(session, following);
   }

   public void Unfollow(Action<IReadOnlyList<ShownInTheChat>> draw, Action<string> tell)
   {
      _draw -= draw;
      _tell -= tell;
      if(_draw is null) Stop();
   }

   void Stop()
   {
      _following?.Cancel();
      _following = null;
      _latest = null;
   }

   async Task Read(ClaudeCodeSessionId session, CancellationTokenSource following)
   {
      try
      {
         await Task.Run(async () =>
         {
            await foreach(var conversation in hub.ConversationAsWritten(session, following.Token))
            {
               var shown = ShownInTheChat.AllOf(conversation);
               await Dispatcher.UIThread.InvokeAsync(() =>
               {
                  if(following.IsCancellationRequested) return;
                  input.Seen(shown);
                  status.Show(shown);
                  _latest = shown;
                  _draw?.Invoke(shown);
               });
            }
         });
      }
      catch(OperationCanceledException) when(following.IsCancellationRequested) { }
      catch(Exception failure)
      {
         if(!following.IsCancellationRequested) _tell?.Invoke($"The chat can't follow the conversation of {session}: {failure.Message}");
      }
      finally
      {
         if(_following == following) _following = null;
         following.Dispose();
      }
   }
}
