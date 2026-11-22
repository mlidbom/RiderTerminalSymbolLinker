using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Hub;
using Cloyd.Saving;
using Cloyd.Sessions;
using System.Runtime.CompilerServices;

namespace Cloyd.Projects;

/// <summary>A tab that opening a project window would show. Reading these choices has no effect
/// on a host or agent. Only the window opening its tabs carries them out.</summary>
abstract record ProjectTabToRestore(SessionHostId OriginalHost)
{
   public sealed record Reattach(SessionHostOfTheHub Host, SavedSessionTab Saved) : ProjectTabToRestore(Host.Id);
   public sealed record Resume(SavedSessionTab Saved, Session Session, string Directory) : ProjectTabToRestore(Saved.SessionHost);

   /// <summary>A closing host or one attached by another application cannot be claimed when restoring tabs.</summary>
   public static bool CanReattach(SessionsOfCloyd sessions, SessionHostOfTheHub host) => !sessions.Ending(host.Id) && sessions.CanRestore(host);

   /// <summary>Yields the saved tabs in their previous order. A missing host contributes a tab
   /// only while its latest session can still be resumed from disk.</summary>
   public static async IAsyncEnumerable<ProjectTabToRestore> FromSavedTabs(SessionsOfCloyd sessions, IReadOnlyList<SessionHostOfTheHub> hosts,
      IEnumerable<SavedSessionTab> savedTabs, [EnumeratorCancellation] CancellationToken cancellation, bool freshRecordings = false)
   {
      var byId = hosts.ToDictionary(it => it.Id);
      var included = new HashSet<SessionHostId>();
      var resuming = new HashSet<SessionId>();
      var resumable = new ResumableSessions(sessions, cancellation, freshRecordings);
      foreach(var saved in savedTabs)
      {
         cancellation.ThrowIfCancellationRequested();
         if(!included.Add(saved.SessionHost) || sessions.Ending(saved.SessionHost)) continue;
         if(byId.TryGetValue(saved.SessionHost, out var host))
         {
            if(CanReattach(sessions, host)) yield return new Reattach(host, saved);
         }
         else
         {
            var (last, folder) = await Task.Run(() =>
            {
               var latest = sessions.SavedForResume(saved);
               return (latest, Directory.Exists(latest.SessionDirectory) ? latest.SessionDirectory! : saved.Directory);
            }, cancellation);
            if(last.Session is {} session && resuming.Add(session.Id) && await resumable.Include(session) && !sessions.Ending(saved.SessionHost))
               yield return new Resume(last, session, folder);
         }
      }
   }

   public static bool CanShowLabelledHost(SessionsOfCloyd sessions, SessionHostOfTheHub host, Func<Project, bool> projects) =>
      host.Project is {} project && projects(project) && CanReattach(sessions, host);
}
