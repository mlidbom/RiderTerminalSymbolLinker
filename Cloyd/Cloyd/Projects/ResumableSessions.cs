using Cloyd.ClaudeCode;
using Cloyd.Codex;
using Cloyd.Hub;
using Cloyd.Sessions;

namespace Cloyd.Projects;

/// <summary>
/// The sessions a <see cref="ProjectWindow"/> can resume as it brings back a saved tab whose host is gone, as after a
/// reboot: a Claude Code session while the Claude Code folder the hub serves holds its transcript, and a Codex session
/// while the hub lists it on disk. A session that can't be resumed leaves its tab out, as its agent CLI would fail to
/// resume it.
/// </summary>
/// <remarks>
/// It asks <paramref name="hub"/> once for each agent CLI, as the first tab of that agent CLI needs it, so that a window
/// whose hosts all run asks nothing.
/// </remarks>
sealed class ResumableSessions(TheHub hub, CancellationToken cancellation)
{
   readonly Lazy<Task<ClaudeCodeFolder>> _claudeCodeFolder = new(() => hub.ClaudeCodeFolderItServes(cancellation));
   readonly Lazy<Task<IReadOnlyList<SessionOnDisk>>> _onDisk = new(() => hub.SessionsOnDisk(cancellation));

   /// <summary>Whether <paramref name="session"/> can be resumed.</summary>
   public async Task<bool> Include(Session session) => session switch
   {
      ClaudeCodeSession claudeCodes => (await _claudeCodeFolder.Value).HoldsTheTranscriptOf(claudeCodes.Id),
      CodexSession codexs => (await _onDisk.Value).Any(it => it.Session.Id == codexs.Id),
      _ => throw new ArgumentOutOfRangeException(nameof(session), session, "A session of an agent CLI that Cloyd doesn't resume.")
   };
}
