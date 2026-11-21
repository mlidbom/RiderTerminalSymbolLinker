using System.Text.Json;
using ClaudeTooling.Hub.LocalApi.Contract;
using ClaudeTooling.Hub.LocalApi.Contract.ListingSessions;
using ClaudeTooling.Hub.LocalApi.Contract.RunningSessions;
using Cloyd.ClaudeCode;
using Cloyd.Hub;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Hub;

/// <summary>
/// The hub's lists of the running sessions and the sessions on disk, holding a Codex session beside a Claude Code one, as
/// <see cref="RunningSession.EachClaudeCodeIn"/> and <see cref="TranscribedSession.EachClaudeCodeSessionIn"/> read them:
/// as a hub that serves Codex sessions will list them, before Cloyd's tabs show any. A Codex session taken for Claude
/// Code's would be resumed with <c>claude --resume</c>, which knows nothing of it.
/// </summary>
public class When_the_hub_lists_sessions_of_an_agent_CLI_whose_sessions_Cloyd_shows_none_of
{
   static readonly ClaudeCodeSessionId ClaudeCodes = new("1d24f54b-3002-4132-8a65-4d7dc8977525");

   const string RunningSessions = """
      {
        "runningSessions": [
          { "processId": 4711, "sessionId": "1d24f54b-3002-4132-8a65-4d7dc8977525", "agentCli": "claudeCode", "status": "idle",
            "statusSince": "2026-11-21T16:00:00Z", "waitingFor": null, "name": "Claude Code's", "directory": "C:\\Dev\\TheProject",
            "sessionHost": null, "statusLine": null, "turnUnderWay": null, "backgroundWork": null },
          { "processId": 4712, "sessionId": "01a1fe6b-c285-7962-a6ae-85f69a3a51f3", "agentCli": "codex", "status": "busy",
            "statusSince": "2026-11-21T16:00:00Z", "waitingFor": null, "name": "Codex's", "directory": "C:\\Dev\\TheProject",
            "sessionHost": null, "statusLine": null, "turnUnderWay": null, "backgroundWork": null }
        ]
      }
      """;

   const string SessionsOnDisk = """
      {
        "sessions": [
          { "sessionId": "1d24f54b-3002-4132-8a65-4d7dc8977525", "agentCli": "claudeCode", "title": "Claude Code's", "lastPrompt": "Fix it",
            "firstPrompt": null, "directory": "C:\\Dev\\TheProject", "branch": "main", "lastUsed": "2026-11-21T16:00:00Z",
            "runByAProgram": false, "hasAConversationToResume": true },
          { "sessionId": "01a1fe6b-c285-7962-a6ae-85f69a3a51f3", "agentCli": "codex", "title": "Codex's", "lastPrompt": null,
            "firstPrompt": "Reply with the single word PONG.", "directory": "C:\\Dev\\TheProject", "branch": "main",
            "lastUsed": "2026-11-21T16:01:00Z", "runByAProgram": false, "hasAConversationToResume": true }
        ]
      }
      """;

   readonly IReadOnlyList<RunningSession> _running = RunningSession.EachClaudeCodeIn(JsonSerializer.Deserialize<RunningSessionsAnswer>(RunningSessions, LocalApiProtocol.Json)!);
   readonly IReadOnlyList<TranscribedSession> _onDisk = TranscribedSession.EachClaudeCodeSessionIn(JsonSerializer.Deserialize<SessionsAnswer>(SessionsOnDisk, LocalApiProtocol.Json)!);

   [XF] public void of_the_running_sessions_it_reads_only_the_Claude_Code_one() => _running.Select(it => it.SessionId).Must().SequenceEqual([ClaudeCodes]);

   [XF] public void of_the_sessions_on_disk_it_reads_only_the_Claude_Code_one() => _onDisk.Select(it => it.Id).Must().SequenceEqual([ClaudeCodes]);
}
