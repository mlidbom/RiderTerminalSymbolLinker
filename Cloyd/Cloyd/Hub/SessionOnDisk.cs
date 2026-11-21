using ClaudeTooling.Hub.LocalApi.Contract;
using ClaudeTooling.Hub.LocalApi.Contract.ListingSessions;
using Cloyd.ClaudeCode;

namespace Cloyd.Hub;

/// <summary>
/// A session Claude Code has a transcript of, as the hub's <c>GET /sessions</c> lists it: what its transcript tells of it.
/// Whether it runs is for <see cref="TheHub.RunningSessions"/> to tell.
/// </summary>
/// <param name="Id">The session's id, which names its transcript, and which <c>claude --resume</c> takes.</param>
/// <param name="Title">The title Claude Code shows for it: the one Magnus gave it with <c>/rename</c>, else Claude Code's own. Null when it has neither.</param>
/// <param name="LastPrompt">Magnus's last prompt in it, on one line, cut at 220 characters: null when he gave none.</param>
/// <param name="Directory">The directory it last ran in: a session resumed elsewhere goes on in the same transcript. Null when its transcript records none.</param>
/// <param name="Branch">The git branch checked out in <paramref name="Directory"/> as it last ran: null outside git.</param>
/// <param name="LastUsed">When Claude Code last wrote its transcript, in UTC.</param>
/// <param name="RunByAProgram">Whether a tool or a script ran it, with <c>claude -p</c> or the Agent SDK, rather than Magnus in a console.</param>
/// <param name="HasAConversationToResume">Whether <c>claude --resume</c> can resume it: its transcript has a prompt or a reply.</param>
sealed record SessionOnDisk(ClaudeCodeSessionId Id,
                                 string? Title,
                                 string? LastPrompt,
                                 string? Directory,
                                 string? Branch,
                                 DateTime LastUsed,
                                 bool RunByAProgram,
                                 bool HasAConversationToResume)
{
   /// <summary>
   /// The Claude Code sessions <paramref name="answer"/> lists. A session of another agent CLI is left out, as the hub's
   /// contract has a reader do with one it doesn't know.
   /// </summary>
   public static IReadOnlyList<SessionOnDisk> EachClaudeCodeSessionIn(SessionsAnswer answer) =>
      //wip: until the history picker lists Codex sessions too, with Codex's tabs (the hub's protocol 2).
      [.. answer.Sessions.Where(it => it.AgentCli == AgentCliNames.ClaudeCode).Select(InThe)];

   /// <summary>The session as the hub's answer <paramref name="session"/> lists it.</summary>
   static SessionOnDisk InThe(SessionInTheAnswer session) =>
      new(new ClaudeCodeSessionId(session.SessionId), session.Title, session.LastPrompt, session.Directory, session.Branch, session.LastUsed, session.RunByAProgram, session.HasAConversationToResume);
}
