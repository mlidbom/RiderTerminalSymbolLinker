using Avalonia.Controls;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Chat.Status;
using Cloyd.Chat.Views;
using Cloyd.ClaudeCode;
using Cloyd.Hub;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Status;

/// <summary>
/// The line under a chat's input box, shown a Claude Code that runs background work of several kinds while Claude works,
/// as the hub tells it as of the last turn's end: two shells, a monitor, and a prompt scheduled to wake the session.
/// Magnus, 2026-11-21: "I have no way in the new UI to see that a session in waiting on something". Made here, for kinds
/// the stand-in for Claude Code doesn't run: what it does run is in <see cref="When_Claude_runs_a_shell_command_in_the_background"/>.
/// </summary>
public class When_a_chats_status_line_shows_what_runs_in_the_background : SpecificationOpeningWindows
{
   static readonly BackgroundWork WhatRuns = new(
      [new("shell", "Run ping in background", "ping -n 40 127.0.0.1", AgentType: null),
       new("monitor", "Watch the CI run until it ends", Command: null, AgentType: null),
       new("shell", "Build the solution", "dotnet build", AgentType: null)],
      [new("37 14 21 11 *", Recurring: false, "Check the CI run")]);

   readonly StatusLineOfTheChat _statusLine;

   public When_a_chats_status_line_shows_what_runs_in_the_background() => _statusLine = Once(() =>
   {
      var window = Open(() => new Window { Width = 840, Height = 80, Content = new StatusLineOfTheChat(), Resources = { MergedDictionaries = { ColoursOfTheChat.AsResources() } } });
      var statusLine = OnTheUIThread(() => (StatusLineOfTheChat)window.Content!);
      OnTheUIThread(() => statusLine.Show(new RunningSession(new ClaudeCodeProcessId(4711), new ClaudeSessionId(Guid.NewGuid().ToString()), "a session", new SessionHostId(Guid.NewGuid().ToString()),
                                                             new StatusOfAClaudeCode("busy", WaitingFor: null), StatusLine: null, TurnUnderWay: null, WhatRuns)));
      Render(window, "a-chats-status-line-with-background-work");
      return statusLine;
   });

   /// <summary>As Claude Code's TUI counts it beside its input box, such as "· 1 shell · ↓ to manage".</summary>
   [XF] public void it_counts_what_runs_by_kind_the_scheduled_prompts_last() => OnTheUIThread(() => _statusLine.BackgroundWork).Must().Be("2 shells · 1 monitor · 1 scheduled prompt");

   [XF] public void its_tip_lists_each_with_what_it_does_and_a_shells_command() =>
      OnTheUIThread(() => _statusLine.TipOfTheBackgroundWork).Must().Be("""
                                                                        shell: Run ping in background
                                                                           ping -n 40 127.0.0.1
                                                                        monitor: Watch the CI run until it ends
                                                                        shell: Build the solution
                                                                           dotnet build
                                                                        scheduled prompt, once at 37 14 21 11 *: Check the CI run
                                                                        """.ReplaceLineEndings("\n"));
}
