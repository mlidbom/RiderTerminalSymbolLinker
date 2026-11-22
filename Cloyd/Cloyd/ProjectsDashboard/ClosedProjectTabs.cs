using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Projects;
using Cloyd.Sessions;

namespace Cloyd.ProjectsDashboard;

/// <summary>The tabs Cloyd would bring back for the project windows that are closed. The dashboard
/// reads the restoration choices without attaching a terminal or resuming an agent.</summary>
sealed class ClosedProjectTabs(ProjectWindows windows)
{
   public IReadOnlySet<SessionHostId> Hosts { get; private set; } = new HashSet<SessionHostId>();
   public IReadOnlyDictionary<SessionId, ProjectTabToRestore.Resume> ToResume { get; private set; } = new Dictionary<SessionId, ProjectTabToRestore.Resume>();
   ClosedProjectTabSources? _lastPublished;
   ClosedProjectTabSources? _readingFrom;
   bool _changedDuringRead;
   DateTime _readAgain;

   public async Task Read(Action changed, CancellationToken cancellation)
   {
      var sessions = windows.Sessions;
      if(!sessions.HasReadHosts || cancellation.IsCancellationRequested) return;
      var sources = new ClosedProjectTabSources(windows);
      if(_readingFrom is {} pending)
      {
         _changedDuringRead |= !sources.Matches(pending);
         return;
      }
      if(DateTime.UtcNow < _readAgain && sources.Matches(_lastPublished)) return;
      try
      {
         while(true)
         {
            cancellation.ThrowIfCancellationRequested();
            _readingFrom = sources;
            _changedDuringRead = false;
            var (reattach, resume) = await ReadChoices(sources, cancellation);
            cancellation.ThrowIfCancellationRequested();
            var current = new ClosedProjectTabSources(windows);
            if(!_changedDuringRead && sources.Matches(current))
            {
               Hosts = reattach;
               ToResume = resume;
               _lastPublished = current;
               _readAgain = DateTime.UtcNow.AddSeconds(30);
               changed();
               return;
            }
            sources = current;
         }
      }
      finally { _readingFrom = null; }
   }

   async Task<(HashSet<SessionHostId>, Dictionary<SessionId, ProjectTabToRestore.Resume>)> ReadChoices(ClosedProjectTabSources sources, CancellationToken cancellation)
   {
      var sessions = windows.Sessions;
      var saved = await Task.Run(() => sessions.Folder.SavedProjectWindows.TabsOfClosedWindows(sources.OpenProjects).ToArray(), cancellation);
      var reattach = new HashSet<SessionHostId>();
      var resume = new Dictionary<SessionId, ProjectTabToRestore.Resume>();
      await foreach(var tab in ProjectTabToRestore.FromSavedTabs(sessions, sources.Hosts, saved, cancellation))
         switch(tab)
         {
            case ProjectTabToRestore.Reattach live: reattach.Add(live.Host.Id); break;
            case ProjectTabToRestore.Resume stopped: resume.Add(stopped.Session.Id, stopped); break;
         }
      return (reattach, resume);
   }
}
