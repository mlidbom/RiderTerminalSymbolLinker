using Cloyd.ClaudeCode;
using Cloyd.Hub;
using Cloyd.Projects;
using Cloyd.Sessions;

namespace Cloyd.History;

/// <summary>
/// The sessions Claude Code has transcripts of, most recently used first, each with where it is now: what the
/// <see cref="ClaudeSessionHistoryPicker"/> lists, as it was when the picker opened. Left out are the sessions with no
/// conversation to resume, and those a tool or a script ran with <c>claude -p</c>.
/// </summary>
sealed class ClaudeSessionHistory
{
   ClaudeSessionHistory(IReadOnlyList<ClaudeSessionHistoryEntry> entries, ProjectWorktrees worktrees)
   {
      Entries = entries;
      Worktrees = worktrees;
   }

   /// <summary>Every session of the history, most recently used first.</summary>
   public IReadOnlyList<ClaudeSessionHistoryEntry> Entries { get; }

   /// <summary>The folders the scopes cover for the project the picker opened in.</summary>
   public ProjectWorktrees Worktrees { get; }

   /// <summary>The entries <paramref name="scope"/> covers that match <paramref name="filter"/>, most recently used first.</summary>
   public IReadOnlyList<ClaudeSessionHistoryEntry> Listed(ClaudeSessionHistoryScope scope, string filter) =>
      [.. Entries.Where(it => Worktrees.Cover(scope, it.Directory) && it.Matches(filter))];

   /// <summary>
   /// Reads the history for <paramref name="project"/>'s picker: the transcripts, the Claude Codes that run, and the
   /// session hosts, from <paramref name="hub"/>; the project's worktrees from git; and the tabs of Cloyd's
   /// <paramref name="windows"/>. On the UI thread, where the tabs are.
   /// </summary>
   public static async Task<ClaudeSessionHistory> ReadFor(Project project, ProjectWindows windows, TheHub hub, CancellationToken cancellation)
   {
      var transcribed = hub.TranscribedSessions(cancellation);
      var running = hub.RunningSessions(cancellation);
      var hosts = hub.SessionHosts(cancellation);
      var worktrees = ProjectWorktrees.Of(project, cancellation);
      await Task.WhenAll(transcribed, running, hosts, worktrees);
      var tabs = windows.Open.SelectMany(window => window.Tabs.Select(tab => (Window: window, Tab: tab))).ToList();

      var entries = transcribed.Result
                               .Where(it => it is { HasAConversationToResume: true, RunWithClaudeP: false })
                               .Select(it => new ClaudeSessionHistoryEntry(it, WhereIs(it.Id)));
      return new ClaudeSessionHistory([.. entries], worktrees.Result);

      WhereTheSessionIs WhereIs(ClaudeSessionId session)
      {
         if(running.Result.FirstOrDefault(it => it.SessionId == session) is {} runningIt) return WhereTheRunning(runningIt);
         if(tabs.FirstOrDefault(it => it.Tab.RunsOrRanLast(session)) is ({} window, {} tab)) return new WhereTheSessionIs.ExitedInATab(window, tab);
         return WhereTheSessionIs.NotRunning.Anywhere;
      }

      WhereTheSessionIs WhereTheRunning(RunningSession session)
      {
         if(tabs.FirstOrDefault(it => it.Tab.Host.Id == session.SessionHost) is ({} window, {} tab)) return new WhereTheSessionIs.RunningInATab(window, tab);
         if(hosts.Result.FirstOrDefault(it => it.Id == session.SessionHost) is {} host) return new WhereTheSessionIs.RunningInAHostNoTabShows(host);
         return new WhereTheSessionIs.RunningOutsideASessionHost(session.Process);
      }
   }
}
