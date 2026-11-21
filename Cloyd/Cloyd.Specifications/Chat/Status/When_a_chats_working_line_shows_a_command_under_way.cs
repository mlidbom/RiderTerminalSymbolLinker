using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using ClaudeTooling.Hub.LocalApi.Contract.Conversations;
using ClaudeTooling.Hub.LocalApi.Contract.Conversations.Messages;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Chat;
using Cloyd.Chat.Status;
using Cloyd.Chat.ToolCalls;
using Cloyd.Chat.Views;
using Cloyd.ClaudeCode;
using Cloyd.Hub;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Status;

/// <summary>
/// The lines over a chat's input box, shown a busy Claude Code whose turn the hub tells nothing of, as one that doesn't
/// load the <c>tooling-commands</c> mod, and a conversation whose last turn ran a command and runs another of four lines:
/// the command under way, as Claude described it, and its first lines under it, as Claude Code's own UI shows a call that
/// runs (Magnus, 2026-11-21: "so that I can see what the agent is doing right now"); then the turn's line, apart from it
/// ("I would like it separated like in the CLI"). Made here, as the stand-in for Claude Code writes a call and its result
/// together.
/// </summary>
public class When_a_chats_working_line_shows_a_command_under_way : SpecificationOpeningWindows
{
   const string Description = "Finding other repositories that use the stand-in's busy prompt";
   const string Command = "ls /c/Dev\ngrep -rln -i \"Be busy for\" /c/Dev --include=*.cs\n\nhead -20\ntail -5";

   readonly WorkingLineOfTheChat _workingLine;

   public When_a_chats_working_line_shows_a_command_under_way() => _workingLine = Once(() =>
   {
      var window = Open(() => new Window { Width = 840, Height = 120, Content = new WorkingLineOfTheChat(), Resources = { MergedDictionaries = { ColoursOfTheChat.AsResources() } } });
      var workingLine = OnTheUIThread(() => (WorkingLineOfTheChat)window.Content!);
      OnTheUIThread(() =>
      {
         workingLine.Show(new RunningSession(new ClaudeCodeProcessId(4711), new ClaudeSessionId(Guid.NewGuid().ToString()), "a session", new SessionHostId(Guid.NewGuid().ToString()),
                                             new StatusOfAClaudeCode("busy", WaitingFor: null), StatusLine: null, TurnUnderWay: null));
         workingLine.Show([new ShownInTheChat.Prompt("prompt", "Which repositories use it?"),
                           new ShownInTheChat.RunOfToolCalls([ACall("call-1", "Bash", new { command = "cd /c/Dev && ls" }, done: true), ACall("call-2", "Bash", new { command = Command, description = Description }, done: false)])]);
      });
      Render(window, "a-chats-working-line");
      return workingLine;
   });

   [XF] public void the_call_under_way_shows_as_Claude_described_it_with_how_long_it_has_run() =>
      OnTheUIThread(() => _workingLine.CallUnderWay).Must().Satisfy(it => it is not null && Regex.IsMatch(it, $@"^{Regex.Escape(Description)} · \ds$"));

   [XF] public void under_it_the_first_three_lines_of_the_command_show_but_the_empty_one_the_last_with_an_ellipsis_as_more_follow() =>
      OnTheUIThread(() => _workingLine.WhatTheCallUnderWayWasGiven).Must().Be("└ $ ls /c/Dev\n  grep -rln -i \"Be busy for\" /c/Dev --include=*.cs\n  head -20…");

   [XF] public void the_turns_line_says_Claude_works_how_long_it_has_and_how_many_commands_the_turn_ran() =>
      OnTheUIThread(() => _workingLine.Says).Must().Satisfy(it => it is not null && Regex.IsMatch(it, $@"^{Regex.Escape(WorkingLineOfTheChat.Working)} \(\ds · 2 commands\)$"));

   static ToolCallInTheChat ACall(string id, string tool, object input, bool done) =>
      new(new MessageInTheAnswer.ToolCall(id, "a-session", DateTime.UtcNow, $"toolu_{id}", tool, JsonSerializer.SerializeToElement(input)),
          done ? new ToolResultInTheAnswer($"toolu_{id}", DateTime.UtcNow, "the output", IsError: false, Characters: 10, Images: []) : null);
}
