using System.Text.Json;
using ClaudeTooling.Hub.LocalApi.Contract;
using ClaudeTooling.Hub.LocalApi.Contract.ListingSessions;
using Cloyd.ClaudeCode;
using Cloyd.Codex;
using Cloyd.Hub;
using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Hub;

/// <summary>
/// The hub's list of the sessions on disk, holding a Codex session, a Claude Code one, and one of an agent CLI that Cloyd
/// doesn't know, as <see cref="SessionOnDisk.EachIn"/> reads it: as the hub of protocol 2 lists them, and as a hub of a
/// later protocol may list another agent CLI's. A Codex session taken for Claude Code's would be resumed with
/// <c>claude --resume</c>, which knows nothing of it.
/// </summary>
public class When_the_hub_lists_the_sessions_on_disk_of_each_agent_CLI
{
   const string SessionsOnDisk = """
      {
        "sessions": [
          { "sessionId": "01a1fe6b-c285-7962-a6ae-85f69a3a51f3", "agentCli": "codex", "title": "Codex's", "lastPrompt": null,
            "firstPrompt": "Reply with the single word PONG.", "directory": "C:\\Dev\\TheProject", "branch": "main",
            "lastUsed": "2026-11-21T16:01:00Z", "runByAProgram": false, "hasAConversationToResume": true },
          { "sessionId": "1d24f54b-3002-4132-8a65-4d7dc8977525", "agentCli": "claudeCode", "title": "Claude Code's", "lastPrompt": "Fix it",
            "firstPrompt": null, "directory": "C:\\Dev\\TheProject", "branch": "main", "lastUsed": "2026-11-21T16:00:00Z",
            "runByAProgram": false, "hasAConversationToResume": true },
          { "sessionId": "a-later-agent-clis", "agentCli": "anAgentCliOfALaterProtocol", "title": null, "lastPrompt": null,
            "firstPrompt": null, "directory": "C:\\Dev\\TheProject", "branch": null, "lastUsed": "2026-11-21T15:59:00Z",
            "runByAProgram": false, "hasAConversationToResume": true }
        ]
      }
      """;

   readonly IReadOnlyList<SessionOnDisk> _onDisk = SessionOnDisk.EachIn(JsonSerializer.Deserialize<SessionsAnswer>(SessionsOnDisk, LocalApiProtocol.Json)!);

   [XF] public void it_reads_the_Codex_session_and_the_Claude_Code_one_in_their_order_each_as_its_agent_CLIs_named_as_listed() =>
      _onDisk.Select(it => it.Session).Must().SequenceEqual<Session>([new CodexSession(new CodexSessionId("01a1fe6b-c285-7962-a6ae-85f69a3a51f3"), "Codex's"),
                                                                      new ClaudeCodeSession(new ClaudeCodeSessionId("1d24f54b-3002-4132-8a65-4d7dc8977525"), "Claude Code's")]);

   [XF] public void the_Codex_sessions_first_prompt_is_read() => _onDisk[0].FirstPrompt.Must().Be("Reply with the single word PONG.");

   [XF] public void the_Claude_Code_sessions_last_prompt_is_read() => _onDisk[1].LastPrompt.Must().Be("Fix it");
}
