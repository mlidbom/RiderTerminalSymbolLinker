using System.Text.Json;
using ClaudeTooling.Hub.LocalApi.Contract;
using ClaudeTooling.Hub.LocalApi.Contract.RunningSessions;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.ClaudeCode;
using Cloyd.Codex;
using Cloyd.Hub.RunningSessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Hub;

/// <summary>
/// The hub's list of the running sessions, holding a Claude Code, a Codex, and a session of an agent CLI that Cloyd
/// doesn't know, as <see cref="RunningSession.EachIn"/> reads it: as a hub that serves Codex sessions lists them, and as
/// a hub of a later protocol may list another agent CLI's.
/// </summary>
public class When_the_hub_lists_running_sessions_of_each_agent_CLI
{
   static readonly ClaudeCodeSessionId ClaudeCodes = new("1d24f54b-3002-4132-8a65-4d7dc8977525");
   static readonly CodexSessionId Codexs = new("01a1fe6b-c285-7962-a6ae-85f69a3a51f3");

   const string RunningSessions = """
      {
        "runningSessions": [
          { "processId": 4711, "sessionId": "1d24f54b-3002-4132-8a65-4d7dc8977525", "agentCli": "claudeCode", "status": "idle",
            "statusSince": "2026-11-21T16:00:00Z", "waitingFor": null, "name": "Claude Code's", "directory": "C:\\Dev\\TheProject",
            "sessionHost": "be700cddf8f67965", "statusLine": null, "turnUnderWay": null, "backgroundWork": null },
          { "processId": 4712, "sessionId": "01a1fe6b-c285-7962-a6ae-85f69a3a51f3", "agentCli": "codex", "status": "busy",
            "statusSince": "2026-11-21T16:00:00Z", "waitingFor": null, "name": "Codex's", "directory": "C:\\Dev\\TheProject",
            "sessionHost": "c0de0000c0de0000", "statusLine": null, "turnUnderWay": null, "backgroundWork": null },
          { "processId": 4713, "sessionId": "a-later-agent-clis", "agentCli": "anAgentCliOfALaterProtocol", "status": "idle",
            "statusSince": "2026-11-21T16:00:00Z", "waitingFor": null, "name": null, "directory": "C:\\Dev\\TheProject",
            "sessionHost": null, "statusLine": null, "turnUnderWay": null, "backgroundWork": null }
        ]
      }
      """;

   readonly IReadOnlyList<RunningSession> _running = RunningSession.EachIn(JsonSerializer.Deserialize<RunningSessionsAnswer>(RunningSessions, LocalApiProtocol.Json)!);

   [XF] public void it_reads_the_Claude_Code_and_the_Codex_and_leaves_out_the_agent_CLI_it_doesnt_know() =>
      _running.Select(it => it.Session.Id).Must().SequenceEqual([ClaudeCodes, Codexs]);

   [XF] public void it_reads_the_Claude_Codes_session_as_Claude_Codes_with_its_name() =>
      _running[0].Session.Must().Be(new ClaudeCodeSession(ClaudeCodes, "Claude Code's"));

   [XF] public void it_reads_the_Codexs_session_as_Codexs_with_its_name() =>
      _running[1].Session.Must().Be(new CodexSession(Codexs, "Codex's"));

   [XF] public void it_reads_the_session_host_each_runs_in() =>
      _running.Select(it => it.SessionHost).Must().SequenceEqual([new SessionHostId("be700cddf8f67965"), new SessionHostId("c0de0000c0de0000")]);

   [XF] public void it_reads_the_busy_Codex_as_busy() => _running[1].IsBusy.Must().BeTrue();

   [XF] public void it_reads_the_idle_Claude_Code_as_not_busy() => _running[0].IsBusy.Must().BeFalse();
}
