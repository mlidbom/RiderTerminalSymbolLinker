using ClaudeTooling.SessionHost.Protocol;
using Cloyd.ClaudeCode;

namespace Cloyd.Saving;

/// <summary>
/// A <see cref="Sessions.SessionTab"/> as its <see cref="Projects.ProjectWindow"/> saved it, for the project's next
/// window to bring back: attached to its host again while the host runs, and its session resumed in a new host once the
/// host is gone, as after a reboot.
/// </summary>
/// <param name="SessionHost">The host the tab showed.</param>
/// <param name="Directory">The directory the host's shell started in: where a new host resumes the session.</param>
/// <param name="Session">The session that ran in the host last: null when none had, as before Claude Code recorded its session.</param>
sealed record SavedSessionTab(SessionHostId SessionHost, string Directory, ClaudeSession? Session);
