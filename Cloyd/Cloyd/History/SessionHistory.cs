using Cloyd.Hub;
using Cloyd.Projects;
using Cloyd.Sessions;

namespace Cloyd.History;

/// <summary>A reading of the application's <see cref="SessionCatalogue"/> for a history picker.
/// Each entry remembers where it was when the picker opened. New agents with no recorded history come
/// first, followed by recordings in order of their latest use.</summary>
sealed class SessionHistory(IReadOnlyList<SessionHistoryEntry> entries, IReadOnlyList<AgentCli> agentClis, ProjectWorktrees? worktrees)
{
   public IReadOnlyList<SessionHistoryEntry> Entries { get; } = entries;
   public IReadOnlyList<AgentCli> AgentClis { get; } = agentClis;
   public IReadOnlyList<SessionHistoryScope> Scopes => worktrees?.Scopes ?? [SessionHistoryScope.AllProjects];

   /// <summary>The entries the picker's scope, selected agent CLIs and search words include.</summary>
   public IReadOnlyList<SessionHistoryEntry> Listed(SessionHistoryScope scope, string filter, IReadOnlySet<AgentCli> agentClisListed)
   {
      var words = filter.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
      return [.. Entries.Where(it => agentClisListed.Contains(it.AgentCli) &&
                                    (scope == SessionHistoryScope.AllProjects || worktrees!.Cover(scope, it.Directory)) && it.Matches(words))];
   }

   /// <summary>Reads the project picker's sessions and the Git scopes it can switch between.</summary>
   public static Task<SessionHistory> ReadFor(Project project, ProjectWindows windows, TheHub hub, CancellationToken cancellation) =>
      Read(windows, hub, ProjectWorktrees.Of(project, cancellation), cancellation);

   /// <summary>Reads every concrete session for Projects Dashboard. Its project and worktree pickers
   /// use membership of their dashboard heading to narrow this same reading.</summary>
   public static Task<SessionHistory> ReadForAllProjects(ProjectWindows windows, TheHub hub, CancellationToken cancellation) =>
      Read(windows, hub, null, cancellation);

   static async Task<SessionHistory> Read(ProjectWindows windows, TheHub hub, Task<ProjectWorktrees>? scope, CancellationToken cancellation)
   {
      var catalogue = windows.Sessions.Catalogue;
      var agents = hub.AgentClisItServes(cancellation);
      await Task.WhenAll(catalogue.Read(cancellation, force: true), agents, scope ?? Task.CompletedTask);

      // Reading a large history can take time. Determine where a choice will go once that history is available.
      var running = hub.RunningSessions(cancellation);
      var hosts = hub.SessionHosts(cancellation);
      await Task.WhenAll(running, hosts);
      cancellation.ThrowIfCancellationRequested();
      var locations = new SessionHistoryLocations(windows, running.Result, hosts.Result);
      var listed = catalogue.WithRunning(running.Result).Values.OrderByDescending(it => it.Recorded?.LastUsed ?? DateTime.MaxValue);
      return new SessionHistory([.. listed.Select(it => new SessionHistoryEntry(it, locations.Of(it.Session.Id)))], agents.Result, scope?.Result);
   }

   public SessionHistory Within(IReadOnlySet<SessionId> sessions) =>
      new(Entries.Where(it => sessions.Contains(it.Id)).ToArray(), AgentClis, worktrees);
}
