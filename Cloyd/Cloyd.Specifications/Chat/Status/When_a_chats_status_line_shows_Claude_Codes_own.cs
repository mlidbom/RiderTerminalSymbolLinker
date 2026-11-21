using Avalonia.Controls;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Chat.Status;
using Cloyd.ClaudeCode;
using Cloyd.Hub;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Status;

/// <summary>
/// The line under a chat's input box, shown a Claude Code whose status line, as the machine's status line command makes
/// it, the hub knows, and its permission mode under it: all of it, as the terminal shows it (Magnus, 2026-11-21: "get all
/// the information from our custom status line in the CLI into the status line here"). Made here, as the stand-in for
/// Claude Code shows the sandbox's hub no status line yet.
/// </summary>
public class When_a_chats_status_line_shows_Claude_Codes_own : SpecificationOpeningWindows
{
   const string TheMachinesStatusLine = "Opus 5.5 · xhigh · context 42% · week 100% · Fable 7% · €361.07 · 72% · home 1 · .claude 24";

   readonly StatusLineOfTheChat _statusLine;

   public When_a_chats_status_line_shows_Claude_Codes_own() => _statusLine = Once(() =>
   {
      var window = Open(() => new Window { Width = 840, Height = 80, Content = new StatusLineOfTheChat() });
      var statusLine = OnTheUIThread(() => (StatusLineOfTheChat)window.Content!);
      OnTheUIThread(() => statusLine.Show(AClaudeCode(new ClaudeCodesStatusLine(TheMachinesStatusLine, "bypass permissions on"), status: "shell")));
      Render(window, "a-chats-status-line");
      return statusLine;
   });

   [XF] public void it_shows_the_status_line_as_the_terminal_does() => OnTheUIThread(() => _statusLine.ClaudeCodesStatusLine).Must().Be(TheMachinesStatusLine);

   [XF] public void under_it_the_permission_mode_as_Claude_Code_shows_it() => OnTheUIThread(() => _statusLine.PermissionMode).Must().Be("⏵⏵ bypass permissions on");

   /// <summary>The Claude Code is idle with a background shell task or monitor running, as the hub's <c>shell</c> status tells. Magnus, 2026-11-21: "add the missing information about background work under the existing two lines under the input box".</summary>
   [XF] public void under_that_that_background_work_runs() => OnTheUIThread(() => _statusLine.BackgroundWork).Must().Be(StatusLineOfTheChat.BackgroundWorkRuns);

   [XF] public void with_no_background_work_running_it_shows_no_line_of_it() =>
      OnTheUIThread(() =>
      {
         var statusLine = new StatusLineOfTheChat();
         statusLine.Show(AClaudeCode(new ClaudeCodesStatusLine(TheMachinesStatusLine, "bypass permissions on"), status: "idle"));
         return statusLine.BackgroundWork;
      }).Must().BeNull();

   [XF] public void with_no_status_line_known_it_shows_none() =>
      OnTheUIThread(() =>
      {
         var statusLine = new StatusLineOfTheChat();
         statusLine.Show(AClaudeCode(statusLine: null, status: "idle"));
         return (statusLine.ClaudeCodesStatusLine, statusLine.PermissionMode);
      }).Must().Be(("", (string?)null));

   static RunningSession AClaudeCode(ClaudeCodesStatusLine? statusLine, string status) =>
      new(new ClaudeCodeProcessId(4711), new ClaudeSessionId(Guid.NewGuid().ToString()), "a session", new SessionHostId(Guid.NewGuid().ToString()), new StatusOfAClaudeCode(status, WaitingFor: null), statusLine, TurnUnderWay: null);
}
