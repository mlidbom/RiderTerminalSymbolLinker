using System.Collections.Immutable;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Hub;
using Cloyd.History;
using Cloyd.Hub.Commands;
using Cloyd.Hub.RunningSessions;
using Cloyd.Projects;
using Cloyd.Pasting;
using Cloyd.Saving;
using Cloyd.SessionHosts;

namespace Cloyd.Sessions;

/// <summary>The application's hosted sessions and its single subscription to the hub's running sessions.
/// All updates and window subscriptions are on the Avalonia UI thread.</summary>
sealed class SessionsOfCloyd(TheHub hub, CloydsFolder folder, ShellOfNewSessions shell)
{
   readonly Dictionary<SessionHostId, HostedSession> _sessions = [];
   readonly HashSet<SessionHostId> _ending = [];
   readonly SavedHostedSessions _saved = folder.SavedHostedSessions;
   CancellationTokenSource? _following;
   int _windows;
   int _inventoryRevision;
   bool _readingArchivedNames;
   ImmutableDictionary<string, string> _peerNames = ImmutableDictionary.Create<string, string>(StringComparer.OrdinalIgnoreCase);

   public TheHub Hub { get; } = hub;
   SessionCatalogue? _catalogue;
   public SessionCatalogue Catalogue => _catalogue ?? throw new InvalidOperationException("No Cloyd window is open.");
   public CloydsFolder Folder { get; } = folder;
   public PastedImages Images { get; } = folder.PastedImages;
   public IReadOnlyList<RunningSession> Running { get; private set; } = [];
   public IReadOnlyList<SessionHostOfTheHub> Hosts { get; private set; } = [];
   public bool OffersCodexChat { get; private set; } = hub.SupportsCodexChat;
   public bool HasReadHosts { get; private set; }
   public bool HasReadRunningSessions { get; private set; }
   public event Action? Changed;
   public event Action<Exception>? Failed;

   public void WindowOpened()
   {
      if(++_windows != 1) return;
      _following = new CancellationTokenSource();
      _catalogue = new SessionCatalogue(Hub, _following.Token);
      _ = FollowRunningSessions(_following.Token);
      _ = FollowHosts(_following.Token);
   }

   public void WindowClosed()
   {
      if(--_windows != 0) return;
      _following!.Cancel();
      _following = null;
      _catalogue = null;
      foreach(var session in _sessions.Values) session.LetGo();
      _sessions.Clear();
      Hosts = [];
      Running = [];
      HasReadHosts = HasReadRunningSessions = false;
      _readingArchivedNames = false;
   }

   public HostedSession Open(SessionHostOfTheHub host, Session? latest = null, NewSession? startedAs = null, ConsoleSize? size = null, SavedSessionTab? previouslySaved = null)
   {
      if(_sessions.TryGetValue(host.Id, out var existing)) return existing;
      var saved = _saved.Read(host.Id) ?? (previouslySaved is null ? LegacyProjectTab(host) : SavedForResume(previouslySaved));
      var session = new HostedSession(this, host, latest, startedAs, size ?? ConsoleSize.Default, saved);
      _sessions.Add(host.Id, session);
      session.Update(Running.FirstOrDefault(it => it.SessionHost == host.Id));
      return session;
   }

   public HostedSession? Known(SessionHostId host) => _sessions.GetValueOrDefault(host);

   /// <summary>Preserves the pinned session's identity and working folder even if no window has yet shown its terminal.</summary>
   public void RememberPinnedHost(SessionHostOfTheHub host, Session? latest, Project? workingFolder)
   {
      var session = Open(host, latest);
      if(latest is not null && session.Running is null) session.RememberSession(latest, workingFolder);
      Save(session);
   }
   public void Include(SessionHostOfTheHub host)
   {
      if(Hosts.Any(it => it.Id == host.Id)) return;
      ++_inventoryRevision;
      Hosts = [.. Hosts, host];
      Changed?.Invoke();
   }
   public bool CanRestore(SessionHostOfTheHub host) => !host.Attached || Known(host.Id)?.HasAttachment == true;
   public bool Ending(SessionHostId id) => _ending.Contains(id);

   public async Task End(SessionHostId id)
   {
      if(!_ending.Add(id)) return;
      try
      {
         await Hub.End(id, CancellationToken.None);
         Known(id)?.HostEnded("the session was ended");
         Folder.SavedProjectWindows.Forget(id);
         ++_inventoryRevision;
         Hosts = Hosts.Where(it => it.Id != id).ToArray();
         Changed?.Invoke();
      }
      finally { _ending.Remove(id); }
   }

   /// <summary>Reads the application’s copy first and imports older project-window drafts on first use.</summary>
   public SavedSessionTab? Saved(SessionHostOfTheHub host) => _saved.Read(host.Id) ?? LegacyProjectTab(host);
   public SavedSessionTab SavedForResume(SavedSessionTab tab) => _saved.Read(tab.SessionHost) ?? tab;

   SavedSessionTab? LegacyProjectTab(SessionHostOfTheHub host) =>
      host.Project is {} project ? Folder.SavedProjectWindows.Of(project).Tabs.FirstOrDefault(it => it.SessionHost == host.Id) : null;

   void Save(HostedSession session) => _saved.Save(session.AsSaved);

   public void Release(SessionView view, bool keepTerminal = false)
   {
      var session = view.Session;
      try { Save(session); }
      catch(Exception failure) { Failed?.Invoke(failure); }
      finally
      {
         view.LetGo();
         if(!session.HasViews && !keepTerminal) session.LetGo();
      }
   }

   public Task<SessionHostOfTheHub> StartHost(Project project, string directory, CancellationToken cancellation) =>
      Hub.StartASessionHost(shell, directory, SessionHostLabel.OfANewHostOf(project), cancellation);

   /// <summary>Starts the one labelled host for a coordinator in its worktree through the hub pinned for this launch.</summary>
   public Task<SessionHostOfTheHub> StartPeerHost(TheHub launchHub, Project project, string worktree, Guid requestId, CancellationToken cancellation) =>
      launchHub.StartASessionHost(shell, worktree, SessionHostLabel.ForAPeer(project, requestId), cancellation);

   public async Task StartIn(SessionView tab, AgentCliCommand command, CancellationToken cancellation)
   {
      try { await command.HaveTheHubTypeItInto(tab.Host, Hub, cancellation); }
      catch(RefusedByTheHub refusal) { tab.Tell($"The hub didn't type {command}: {refusal.Message}"); }
   }

   public void ShowPeerNames(SessionView tab) => tab.Chat?.ShowPeerNames(_peerNames, RequestArchivedPeerNames);

   void RequestArchivedPeerNames()
   {
      if(_readingArchivedNames || _following is null) return;
      _readingArchivedNames = true;
      _ = Read(_following.Token);

      async Task Read(CancellationToken cancellation)
      {
         try
         {
            foreach(var entry in await Hub.SessionsOnDisk(cancellation))
               if(entry.Session is { Name: { Length: > 0 } name } session && !_peerNames.ContainsKey(session.Id.Value))
                  _peerNames = _peerNames.Add(session.Id.Value, name);
            Changed?.Invoke();
         }
         catch(OperationCanceledException) when(cancellation.IsCancellationRequested) { }
         catch(Exception failure) { Failed?.Invoke(failure); }
      }
   }

   async Task FollowRunningSessions(CancellationToken cancellation)
   {
      while(!cancellation.IsCancellationRequested)
      {
         try
         {
            await foreach(var (announcement, running) in Hub.RunningSessionsAsTheyChange(cancellation))
            {
               if(cancellation.IsCancellationRequested) return;
               OffersCodexChat = announcement.Says.SupportsCodexChat;
               HasReadRunningSessions = true;
               Running = running;
               foreach(var item in running)
                  if(item.Session.Name is {Length: > 0} name) _peerNames = _peerNames.SetItem(item.Session.Id.Value, name);
               foreach(var session in _sessions.Values.ToArray())
                  session.Update(running.FirstOrDefault(it => it.SessionHost == session.Host.Id));
               Changed?.Invoke();
            }
         }
         catch(OperationCanceledException) when(cancellation.IsCancellationRequested) { return; }
         catch(Exception failure) { Failed?.Invoke(failure); }
         await Task.Delay(TimeSpan.FromSeconds(1), cancellation).ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.SuppressThrowing);
      }
   }

   async Task FollowHosts(CancellationToken cancellation)
   {
      while(!cancellation.IsCancellationRequested)
      {
         try
         {
            var before = _sessions.Values.Where(it => !it.HasAttachment).ToArray();
            var revision = _inventoryRevision;
            var hosts = await Hub.SessionHosts(cancellation);
            if(cancellation.IsCancellationRequested) return;
            if(revision != _inventoryRevision) continue;
            Hosts = hosts;
            HasReadHosts = true;
            foreach(var session in before.Where(it => !it.HasAttachment))
            {
               var host = hosts.FirstOrDefault(it => it.Id == session.Host.Id);
               if(host is null || host.Attached && session.HasProjectTab)
                  session.HostEnded(host is null ? "the session host exited" : "another client shows the session host");
               if(host is null && !session.HasViews)
               {
                  _sessions.Remove(session.Host.Id);
                  _saved.ForgetInMemory(session.Host.Id);
               }
            }
            foreach(var session in _sessions.Values) Save(session);
            Changed?.Invoke();
         }
         catch(OperationCanceledException) when(cancellation.IsCancellationRequested) { return; }
         catch(Exception failure) { Failed?.Invoke(failure); }
         await Task.Delay(TimeSpan.FromSeconds(2), cancellation).ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.SuppressThrowing);
      }
   }
}
