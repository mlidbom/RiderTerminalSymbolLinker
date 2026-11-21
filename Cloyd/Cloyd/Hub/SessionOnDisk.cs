using ClaudeTooling.Hub.LocalApi.Contract;
using ClaudeTooling.Hub.LocalApi.Contract.ListingSessions;
using Cloyd.ClaudeCode;
using Cloyd.Codex;
using Cloyd.Sessions;

namespace Cloyd.Hub;

/// <summary>
/// A session on disk, as the hub's <c>GET /sessions</c> lists it: a Claude Code session, as its transcript tells of it, or
/// a Codex session, as Codex tells of it. Whether it runs is for <see cref="TheHub.RunningSessions"/> to tell.
/// </summary>
/// <param name="Session">The session, named as its agent CLI shows it: the name Magnus gave it with <c>/rename</c>, else its agent CLI's own.</param>
/// <param name="LastPrompt">Magnus's last prompt in it, on one line, cut at 220 characters: null when he gave none, and for a Codex session, whose listing has none.</param>
/// <param name="FirstPrompt">Magnus's first prompt in it, as Codex previews a session: null for a Claude Code session.</param>
/// <param name="Directory">The directory it last ran in: a session resumed elsewhere goes on in the same session. Null when a Claude Code session's transcript records none.</param>
/// <param name="Branch">The git branch checked out in <paramref name="Directory"/> as it last ran: null outside git.</param>
/// <param name="LastUsed">When the session was last written to, in UTC.</param>
/// <param name="RunByAProgram">Whether a tool or a script gave its prompts, with <c>claude -p</c>, the Agent SDK or <c>codex exec</c>, rather than Magnus in a console.</param>
/// <param name="HasAConversationToResume">Whether its agent CLI can resume it: a Claude Code session's transcript has a prompt or a reply. Every Codex session can.</param>
sealed record SessionOnDisk(Session Session,
                            string? LastPrompt,
                            string? FirstPrompt,
                            string? Directory,
                            string? Branch,
                            DateTime LastUsed,
                            bool RunByAProgram,
                            bool HasAConversationToResume)
{
   /// <summary>
   /// The sessions <paramref name="answer"/> lists, in its order, most recently used first. A session of an agent CLI that
   /// Cloyd doesn't know is left out, as the hub's contract has a reader do.
   /// </summary>
   public static IReadOnlyList<SessionOnDisk> EachIn(SessionsAnswer answer) => [.. answer.Sessions.Select(InThe).OfType<SessionOnDisk>()];

   /// <summary>The session as the hub's answer <paramref name="session"/> lists it: null for one of an agent CLI Cloyd doesn't know.</summary>
   static SessionOnDisk? InThe(SessionInTheAnswer session) =>
      TheSessionIn(session) is {} it ? new(it, session.LastPrompt, session.FirstPrompt, session.Directory, session.Branch, session.LastUsed, session.RunByAProgram, session.HasAConversationToResume) : null;

   static Session? TheSessionIn(SessionInTheAnswer session) => session.AgentCli switch
   {
      AgentCliNames.ClaudeCode => new ClaudeCodeSession(new ClaudeCodeSessionId(session.SessionId), session.Title),
      AgentCliNames.Codex => new CodexSession(new CodexSessionId(session.SessionId), session.Title),
      _ => null
   };
}
