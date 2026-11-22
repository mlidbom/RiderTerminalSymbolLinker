using Avalonia.Threading;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Hub;
using Cloyd.Pasting;
using Cloyd.SessionHosts;
using Cloyd.Sessions;

namespace Cloyd.Terminals;

/// <summary>One terminal emulator and its connection for the lifetime of a hosted session's views.
/// The host is contacted on first layout. Closing the terminal cancels a pending connection and prevents
/// notifications from its relay from closing a later terminal for the same host.</summary>
sealed class TerminalInCloyd : IDisposable
{
   readonly CancellationTokenSource _closed = new();
   readonly SessionHostOfTheHub _host;
   readonly ConsoleSize _size;
   readonly TheHub _hub;
   readonly Action<string> _ended;
   AttachmentToASessionHost? _attachment;
   bool _connecting;
   bool _allowTakeover;

   public TerminalInCloyd(SessionHostOfTheHub host, ConsoleSize size, TheHub hub, PastedImages images, Func<AgentCli> agentCli, Action<string> ended)
   {
      _host = host;
      _size = size;
      _hub = hub;
      _ended = ended;
      Control = new TerminalOfASessionHost(Attach, images, agentCli);
   }

   public TerminalOfASessionHost Control { get; }
   public bool HasAttachment => _attachment is not null || _connecting;

   /// <summary>An explicit request to show this terminal may take it over from an external client.</summary>
   public void AllowTakeover() => _allowTakeover = true;

   async Task<AttachmentToASessionHost?> Attach()
   {
      var closing = _closed.Token;
      _connecting = true;
      try
      {
         if(!_allowTakeover)
         {
            var host = (await _hub.SessionHosts(closing)).FirstOrDefault(it => it.Id == _host.Id);
            if(host is null || host.Attached && !_allowTakeover)
            {
               Ended(host is null ? "the session host exited" : "another client shows the session host");
               return null;
            }
         }

         var attached = await Task.Run(() => AttachmentToASessionHost.TryAttach(_host.Id, _size, Ended), closing);
         if(closing.IsCancellationRequested)
         {
            attached?.Dispose();
            return null;
         }
         if(attached is null) Ended("the session host exited");
         return _attachment = attached;
      }
      catch(OperationCanceledException) when(closing.IsCancellationRequested) { return null; }
      finally { _connecting = false; }

      void Ended(string why) => Dispatcher.UIThread.Post(() =>
      {
         if(!closing.IsCancellationRequested) _ended(why);
      });
   }

   public void Dispose()
   {
      _closed.Cancel();
      Control.Dispose();
      _attachment?.Dispose();
      _closed.Dispose();
   }
}
