using ClaudeTooling.Hub.LocalApi.Contract;
using Cloyd.Hub;
using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Hub;

/// <summary>
/// The announcement of a hub that serves no Codex sessions, as on a machine where npm hasn't installed Codex: it names no
/// Codex folder. The <see cref="NewSessionButton"/> then offers Claude Code alone, with no choice to make.
/// </summary>
public class When_the_hubs_announcement_names_no_Codex_folder
{
   readonly AnnouncementOfTheHub _announcement = new(new WhatTheAnnouncementSays(Protocol: LocalApiProtocol.Version,
                                                                                 Port: 50123,
                                                                                 Token: "the-token",
                                                                                 ProcessId: 4242,
                                                                                 Build: new DateTime(2026, 11, 21, 17, 35, 0, DateTimeKind.Utc),
                                                                                 ClaudeCodeFolder: @"C:\Users\someone\.claude",
                                                                                 CodexFolder: null));

   [XF] public void the_hub_serves_Claude_Code_alone() => _announcement.AgentClisItServes.Must().SequenceEqual([AgentCli.ClaudeCode]);
}

