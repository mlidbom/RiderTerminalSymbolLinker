using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Hub;
using Cloyd.Projects;
using Cloyd.Sessions;

namespace Cloyd.ProjectsDashboard;

/// <summary>The inputs that determine a closed project window's tabs. Capture on the UI thread
/// before reading files or querying the hub. Compare with a fresh capture before publishing the result.</summary>
sealed class ClosedProjectTabSources
{
   readonly HashSet<(SessionHostId Host, bool CanReattach)> _hostStates;
   readonly HashSet<SessionId> _alreadyInUse;
   readonly long _savedWindowsRevision;
   readonly long _savedSessionsRevision;
   readonly long _recordingRevision;

   public ClosedProjectTabSources(ProjectWindows windows)
   {
      var sessions = windows.Sessions;
      OpenProjects = windows.Open.Select(it => it.Project).ToHashSet();
      Hosts = sessions.Hosts;
      _hostStates = Hosts.Select(it => (it.Id, ProjectTabToRestore.CanReattach(sessions, it))).ToHashSet();
      _alreadyInUse = sessions.Running.Select(it => it.Session.Id).ToHashSet();
      _alreadyInUse.UnionWith(windows.Open.SelectMany(it => it.Tabs).Select(it => it.LatestSession?.Id).OfType<SessionId>());
      _savedWindowsRevision = sessions.Folder.SavedProjectWindows.RestorationRevision;
      _savedSessionsRevision = sessions.Folder.SavedHostedSessions.RestorationRevision;
      _recordingRevision = sessions.Catalogue.RecordingRevision;
   }

   public HashSet<Project> OpenProjects { get; }
   public IReadOnlyList<SessionHostOfTheHub> Hosts { get; }

   public bool Matches(ClosedProjectTabSources? other) =>
      other is not null && _savedWindowsRevision == other._savedWindowsRevision &&
      _savedSessionsRevision == other._savedSessionsRevision && _recordingRevision == other._recordingRevision &&
      OpenProjects.SetEquals(other.OpenProjects) && _hostStates.SetEquals(other._hostStates) && _alreadyInUse.SetEquals(other._alreadyInUse);
}
