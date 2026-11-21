using Avalonia.Controls;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Chat.Status;
using Cloyd.ClaudeCode;
using Cloyd.Hub;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Status;

/// <summary>
/// The line under a chat's input box, shown a Claude Code that waits for Magnus in a permission prompt, with the status
/// line it showed last: made here, as the stand-in for Claude Code shows the sandbox's hub no status line.
/// </summary>
public class When_a_chats_status_line_shows_a_Claude_Code_that_waits_for_Magnus : SpecificationOpeningWindows
{
   readonly StatusLineOfTheChat _statusLine;

   public When_a_chats_status_line_shows_a_Claude_Code_that_waits_for_Magnus() => _statusLine = Once(() =>
   {
      var window = Open(() => new Window { Width = 840, Height = 60, Content = new StatusLineOfTheChat() });
      var statusLine = OnTheUIThread(() => (StatusLineOfTheChat)window.Content!);
      OnTheUIThread(() => statusLine.Show(AClaudeCode(new StatusOfAClaudeCode("waiting", "permission prompt"), new ClaudeCodesStatusLine("Opus 5.5", "xhigh", 23.4, "auto", "auto mode on"))));
      Render(window, "a-chats-status-line");
      return statusLine;
   });

   [XF] public void at_its_left_it_tells_Claude_waits_for_him_in_the_terminal() =>
      OnTheUIThread(() => _statusLine.WhatClaudeDoes).Must().Be("Claude waits for you in the terminal: permission prompt");

   [XF] public void at_its_right_it_shows_the_model_the_effort_the_permission_mode_in_short_and_the_context_used() =>
      OnTheUIThread(() => _statusLine.ClaudeCodesStatusLine).Must().Be("Opus 5.5 · xhigh · Auto · 23% context");

   [XF] public void a_permission_mode_the_hub_doesnt_know_shows_as_Claude_Code_shows_it() =>
      new ClaudeCodesStatusLine("Opus 5.5", EffortLevel: null, ContextUsedPercentage: null, PermissionMode: null, "some new mode on").AsShown.Must().Be("Opus 5.5 · some new mode on");

   [XF] public void with_no_status_line_known_it_shows_none() =>
      OnTheUIThread(() =>
      {
         var statusLine = new StatusLineOfTheChat();
         statusLine.Show(AClaudeCode(new StatusOfAClaudeCode("idle", WaitingFor: null), statusLine: null));
         return statusLine.ClaudeCodesStatusLine;
      }).Must().Be("");

   static RunningSession AClaudeCode(StatusOfAClaudeCode status, ClaudeCodesStatusLine? statusLine) =>
      new(new ClaudeCodeProcessId(4711), new ClaudeSessionId(Guid.NewGuid().ToString()), "a session", new SessionHostId(Guid.NewGuid().ToString()), status, statusLine);
}
