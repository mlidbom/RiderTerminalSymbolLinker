using Cloyd.Hub;
using Cloyd.Hub.RunningSessions;
using Cloyd.Projects;
using Cloyd.Sessions;

namespace Cloyd.History;

/// <summary>
/// The sessions on disk of every agent CLI the hub serves, Claude Code's and Codex's in one list, most recently used
/// first, each with where it is now: what the <see cref="SessionHistoryPicker"/> lists, as it was when the picker opened.
/// Left out are the sessions with no conversation to resume, and those a tool or a script ran, as with <c>claude -p</c>
/// or <c>codex exec</c>.
/// </summary>
sealed class SessionHistory
{
   SessionHistory(IReadOnlyList<SessionHistoryEntry> entries, IReadOnlyList<AgentCli> agentClis, ProjectWorktrees worktrees)
   {
      Entries = entries;
      AgentClis = agentClis;
      Worktrees = worktrees;
   }

   /// <summary>Every session of the history, most recently used first.</summary>
   public IReadOnlyList<SessionHistoryEntry> Entries { get; }

   /// <summary>
   /// The agent CLIs whose sessions the history holds: those the hub serves, as its announcement tells
   /// (<see cref="AnnouncementOfTheHub.AgentClisItServes"/>), whether any of their sessions is on disk or not.
   /// </summary>
   public IReadOnlyList<AgentCli> AgentClis { get; }

   /// <summary>The folders the scopes cover for the project the picker opened in.</summary>
   public ProjectWorktrees Worktrees { get; }

   /// <summary>
   /// The entries of <paramref name="agentClis"/> that <paramref name="scope"/> covers and that match
   /// <paramref name="filter"/>, most recently used first.
   /// </summary>
   public IReadOnlyList<SessionHistoryEntry> Listed(SessionHistoryScope scope, string filter, IReadOnlySet<AgentCli> agentClis) =>
      [.. Entries.Where(it => agentClis.Contains(it.AgentCli) && Worktrees.Cover(scope, it.Directory) && it.Matches(filter))];

   /// <summary>
   /// Reads the history for <paramref name="project"/>'s picker: the sessions on disk, the agent CLIs that run them, the
   /// session hosts, and the agent CLIs the hub serves, from <paramref name="hub"/>; the project's worktrees from git; and
   /// the tabs of Cloyd's <paramref name="windows"/>. On the UI thread, where the tabs are.
   /// </summary>
   public static async Task<SessionHistory> ReadFor(Project project, ProjectWindows windows, TheHub hub, CancellationToken cancellation)
   {
      var onDisk = hub.SessionsOnDisk(cancellation);
      var running = hub.RunningSessions(cancellation);
      var hosts = hub.SessionHosts(cancellation);
      var agentClis = hub.AgentClisItServes(cancellation);
      var worktrees = ProjectWorktrees.Of(project, cancellation);
      await Task.WhenAll(onDisk, running, hosts, agentClis, worktrees);
      var tabs = windows.Open.SelectMany(window => window.Tabs.Select(tab => (Window: window, Tab: tab))).ToList();

      var entries = onDisk.Result
                          .Where(it => it is { HasAConversationToResume: true, RunByAProgram: false })
                          .Select(it => new SessionHistoryEntry(it, WhereIs(it.Session.Id)));
      return new SessionHistory([.. entries], agentClis.Result, worktrees.Result);

      WhereTheSessionIs WhereIs(SessionId session)
      {
         if(running.Result.FirstOrDefault(it => it.Session.Id == session) is {} runningIt) return WhereTheRunning(runningIt);
         if(tabs.FirstOrDefault(it => it.Tab.RunsOrRanLast(session)) is ({} window, {} tab)) return new WhereTheSessionIs.ExitedInATab(window, tab);
         return WhereTheSessionIs.NotRunning.Anywhere;
      }

      WhereTheSessionIs WhereTheRunning(RunningSession session)
      {
         if(tabs.FirstOrDefault(it => it.Tab.Host.Id == session.SessionHost) is ({} window, {} tab)) return new WhereTheSessionIs.RunningInATab(window, tab);
         if(hosts.Result.FirstOrDefault(it => it.Id == session.SessionHost) is {} host) return new WhereTheSessionIs.RunningInAHostNoTabShows(host);
         return new WhereTheSessionIs.RunningOutsideASessionHost();
      }
   }
}
