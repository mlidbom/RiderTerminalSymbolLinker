using Cloyd.ClaudeCode;
using Cloyd.Codex;
using Cloyd.Hub.RunningSessions;
using Cloyd.Sessions;

namespace Cloyd.Projects;

/// <summary>
/// The sessions a <see cref="ProjectWindow"/> can resume as it brings back a saved tab whose host is gone, as after a
/// reboot: a Claude Code session while the Claude Code folder the hub serves holds its transcript, and a Codex session
/// while the hub lists it on disk. A session that can't be resumed leaves its tab out, as its agent CLI would fail to
/// resume it.
/// </summary>
/// <remarks>
/// The running inventory, Claude folder and shared Codex catalogue are read only if a missing host needs them.
/// </remarks>
sealed class ResumableSessions(SessionsOfCloyd sessions, CancellationToken cancellation, bool freshRecordings)
{
   readonly Lazy<Task<ClaudeCodeFolder>> _claudeCodeFolder = new(() => sessions.Hub.ClaudeCodeFolderItServes(cancellation));
   readonly Lazy<Task> _recordings = new(() => sessions.Catalogue.Read(cancellation, force: freshRecordings));
   readonly Lazy<Task<IReadOnlyList<RunningSession>>> _running = new(() => sessions.Hub.RunningSessions(cancellation));

   /// <summary>Whether <paramref name="session"/> can be resumed.</summary>
   public async Task<bool> Include(Session session)
   {
      var running = await _running.Value;
      cancellation.ThrowIfCancellationRequested();
      if(running.Any(it => it.IsLiveAgentOf(session.Id)) || sessions.HasProjectTabFor(session.Id)) return false;
      switch(session)
      {
         case ClaudeCodeSession claude:
            var folder = await _claudeCodeFolder.Value;
            return await Task.Run(() => folder.HoldsTheTranscriptOf(claude.Id), cancellation);
         case CodexSession:
            await _recordings.Value;
            cancellation.ThrowIfCancellationRequested();
            return sessions.Catalogue.HasRecording(session.Id);
         default: throw new ArgumentOutOfRangeException(nameof(session), session, "A session of an agent CLI that Cloyd doesn't resume.");
      }
   }
}
