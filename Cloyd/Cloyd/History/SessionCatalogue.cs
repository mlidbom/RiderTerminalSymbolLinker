using Cloyd.Hub;
using Cloyd.Hub.RunningSessions;
using Cloyd.Sessions;

namespace Cloyd.History;

/// <summary>The application's shared session list for one lifetime of its windows. The UI thread owns
/// the catalogue. Concurrent requests share the disk reading while each caller can stop waiting independently.</summary>
sealed class SessionCatalogue(Func<CancellationToken, Task<IReadOnlyList<SessionOnDisk>>> readOnDisk, CancellationToken lifetime)
{
   static readonly TimeSpan BetweenReadings = TimeSpan.FromSeconds(30);
   Dictionary<SessionId, SessionListing> _recorded = [];
   HashSet<SessionId> _programSessions = [];
   IReadOnlyList<RunningSession>? _lastRunning;
   IReadOnlyDictionary<SessionId, SessionListing> _listings = new Dictionary<SessionId, SessionListing>();
   Task? _reading;
   DateTime _readAgain;

   public SessionCatalogue(TheHub hub, CancellationToken lifetime) : this(hub.SessionsOnDisk, lifetime) {}

   public bool HasRead { get; private set; }
   public event Action? Changed;

   /// <summary>Reads the sessions on disk when due, or on request by a history picker. Canceling
   /// <paramref name="cancellation"/> leaves the reading available to the other windows.</summary>
   public Task Read(CancellationToken cancellation, bool force = false)
   {
      cancellation.ThrowIfCancellationRequested();
      lifetime.ThrowIfCancellationRequested();
      if(_reading is {} pending) return pending.WaitAsync(cancellation);
      if(!force && DateTime.UtcNow < _readAgain) return Task.CompletedTask;

      var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
      _reading = completion.Task;
      _ = ReadAndPublish(completion);
      return completion.Task.WaitAsync(cancellation);
   }

   async Task ReadAndPublish(TaskCompletionSource completion)
   {
      try
      {
         var recorded = await readOnDisk(lifetime);
         lifetime.ThrowIfCancellationRequested();
         // The hub lists newest first. A repeated ID contributes its newest record.
         var interactive = new Dictionary<SessionId, SessionListing>();
         var programs = new HashSet<SessionId>();
         foreach(var record in recorded.DistinctBy(it => it.Session.Id))
         {
            if(record.RunByAProgram) programs.Add(record.Session.Id);
            else interactive.Add(record.Session.Id, SessionListing.FromDisk(record));
         }
         _recorded = interactive;
         _programSessions = programs;
         _lastRunning = null;
         HasRead = true;
         Changed?.Invoke();
         completion.SetResult();
      }
      catch(OperationCanceledException canceled) { completion.SetCanceled(canceled.CancellationToken); }
      catch(Exception failure) { completion.SetException(failure); }
      finally
      {
         _readAgain = DateTime.UtcNow + BetweenReadings;
         _reading = null;
      }
   }

   /// <summary>One entry per concrete agent session. The running agent supplies its current name and
   /// working folder. Sessions started by programs and recordings with nothing to resume are omitted,
   /// except that an interactive running agent can appear before its first prompt.</summary>
   public IReadOnlyDictionary<SessionId, SessionListing> WithRunning(IReadOnlyList<RunningSession> running)
   {
      if(ReferenceEquals(running, _lastRunning)) return _listings;
      var listings = _recorded.Where(it => it.Value.Recorded?.HasAConversationToResume == true)
                              .ToDictionary();
      foreach(var agent in running.DistinctBy(it => it.Session.Id))
      {
         if(_programSessions.Contains(agent.Session.Id)) continue;
         var recorded = _recorded.GetValueOrDefault(agent.Session.Id)?.Recorded;
         listings[agent.Session.Id] = SessionListing.WhileRunning(agent, recorded);
      }
      _listings = listings;
      _lastRunning = running;
      return _listings;
   }
}
