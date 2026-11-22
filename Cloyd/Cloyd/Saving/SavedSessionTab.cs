using System.Text.Json.Serialization;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Chat.Writing;
using Cloyd.Sessions;

namespace Cloyd.Saving;

/// <summary>
/// A <see cref="Sessions.SessionTab"/> as its <see cref="Projects.ProjectWindow"/> saved it, for the project's next
/// window to bring back: attached to its host again while the host runs, and its session resumed in a new host once the
/// host is gone, as after a reboot.
/// </summary>
/// <param name="SessionHost">The host the tab showed.</param>
/// <param name="Directory">The directory the host's shell started in, used for resumption when the agent's last folder is unavailable.</param>
/// <param name="Session">
/// The session that ran in the host last, of whichever agent CLI: null when none had, as before Claude Code recorded its
/// session.
/// </param>
/// <param name="Draft">
/// What the Claude Code chat's input box held, which comes back into it: null in what a Cloyd saved before its chat had an
/// input box.
/// </param>
/// <param name="CodexDrafts">Each Codex session's own unsent draft, including uncertain submissions.</param>
/// <param name="SessionDirectory">The agent's last working folder when it differs from the host's initial directory.</param>
sealed record SavedSessionTab(SessionHostId SessionHost, string Directory, Session? Session, IReadOnlyList<PartOfAMessage>? Draft = null,
                              [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SavedCodexDraft>? CodexDrafts = null,
                              [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SessionDirectory = null);
