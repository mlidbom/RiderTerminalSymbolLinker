using Cloyd.LocalApi.Contract;

namespace Cloyd.Sessions;

/// <summary>Observes every running-session update in the host, beginning before the launch command is sent.
/// Two different IDs can appear before the command acknowledgement or before the waiting UI continuation runs.</summary>
sealed class FirstSessionOfAPeerLaunch : IDisposable
{
   readonly HostedSession _host;
   readonly SessionToStartForAPeer _request;
   readonly PeerSessionStarted? _recorded;
   string? _firstAddress;
   bool _replaced;
   TaskCompletionSource _nextObservation = new(TaskCreationOptions.RunContinuationsAsynchronously);

   public FirstSessionOfAPeerLaunch(HostedSession host, SessionToStartForAPeer request, PeerSessionStarted? recorded)
   {
      _host = host;
      _request = request;
      _recorded = recorded;
      _firstAddress = recorded?.Address;
      _host.Changed += Observe;
      Observe();
   }

   void Observe()
   {
      if(_host.Running?.Session is {} running)
      {
         var address = $"{(running.AgentCli == AgentCli.Codex ? "codex" : "claude")}:{running.Id.Value}";
         _firstAddress ??= address;
         _replaced |= address != _firstAddress || running.AgentCli != _request.Session.AgentCli;
      }
      var previous = _nextObservation;
      _nextObservation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
      previous.TrySetResult();
   }

   public async Task<Session> Confirm(CancellationToken cancellation)
   {
      while(true)
      {
         cancellation.ThrowIfCancellationRequested();
         if(_replaced)
            throw new InvalidOperationException($"The original helper {_firstAddress} changed in host {_host.Host.Id} before confirmation. No brief was sent.");
         if(_host.Running?.Session is {} current)
         {
            if(_recorded is not null || current.AgentCli == AgentCli.Codex || current.Name == _request.Name) return current;
            if(!string.IsNullOrEmpty(current.Name))
               throw new InvalidOperationException($"The original host shows {current.Name}, expected {_request.Name}. No brief was sent.");
         }
         await _nextObservation.Task.WaitAsync(cancellation);
      }
   }

   public void Dispose() => _host.Changed -= Observe;
}
