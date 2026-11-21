using System.Text.RegularExpressions;
using Cloyd.Sessions;
using Cloyd.Specifications.Chat.Writing;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Status;

/// <summary>
/// Magnus has the stand-in for Claude Code in a chat's tab run a shell command in the background for 8 seconds, as Claude
/// runs one with the Bash tool's <c>run_in_background</c>: the turn that starts it ends, and the shell goes on, the session
/// waiting on it. The stand-in tells the sandbox's hub of it at the turn's end, as Claude Code tells a <c>Stop</c> hook.
/// Magnus, 2026-11-21: "I have no way in the new UI to see that a session in waiting on something".
/// </summary>
public class When_Claude_runs_a_shell_command_in_the_background : SpecificationWritingInAChat
{
   const string TheShell = "shell: Wait 8 seconds in the background\n   Start-Sleep -Seconds 8";

   readonly SessionTab _tab;

   public When_Claude_runs_a_shell_command_in_the_background() => _tab = Once(() =>
   {
      var (window, tab) = AChatOfANewSessionIn(Sandbox.Project("the-project"));
      TypeAndSend(window, "Run a background shell for 8 seconds");
      WaitUntil(() => tab.Chat.StatusLine.BackgroundWork is not null && tab.BackgroundWorkMarked is not null,
                "the line under the input box and the tab's header told of the shell", () => $"the line: {tab.Chat.StatusLine.BackgroundWork}; the tab: {tab.BackgroundWorkMarked}");
      Render(window, "a-tab-whose-claude-runs-a-shell-in-the-background");
      return tab;
   });

   /// <summary>As Claude Code's TUI counts it beside its input box: "· 1 shell · ↓ to manage".</summary>
   [XF] public void the_line_under_the_input_box_counts_it_as_Claude_Code_does() => OnTheUIThread(() => _tab.Chat.StatusLine.BackgroundWork).Must().Be("1 shell");

   [XF] public void the_lines_tip_tells_what_the_shell_does_in_Claudes_words_and_its_command() => OnTheUIThread(() => _tab.Chat.StatusLine.TipOfTheBackgroundWork).Must().Be(TheShell);

   /// <summary>While Claude is idle, the marker's place is the spinner's while Claude works.</summary>
   [XF] public void the_tabs_header_marks_that_it_runs_its_tip_telling_what_runs() => OnTheUIThread(() => _tab.BackgroundWorkMarked).Must().Be($"1 shell\n{TheShell}");

   /// <summary>Then the shell's 8 seconds are up, and Claude Code's notification of its end wakes Claude, who replies to it.</summary>
   public class and_then_the_shell_ends : When_Claude_runs_a_shell_command_in_the_background
   {
      public and_then_the_shell_ends() =>
         WaitUntil(() => RepliesIn(_tab).Any(IsTheReplyToTheShellsEnd) && _tab.Chat.StatusLine.BackgroundWork is null && _tab.BackgroundWorkMarked is null && !_tab.ShowsThatClaudeWorks,
                   "Claude replied to the shell's end, and the line under the input box and the tab's marker went",
                   () => $"the replies: {string.Join(" | ", RepliesIn(_tab))}; the line: {_tab.Chat.StatusLine.BackgroundWork}; the tab: {_tab.BackgroundWorkMarked}");

      [XF] public void Claude_replies_to_its_end() => RepliesIn(_tab).Must().Satisfy(it => it.Count(IsTheReplyToTheShellsEnd) == 1);

      [XF] public void the_line_under_the_input_box_goes() => OnTheUIThread(() => _tab.Chat.StatusLine.BackgroundWork).Must().BeNull();

      [XF] public void the_marker_in_the_tabs_header_goes() => OnTheUIThread(() => _tab.BackgroundWorkMarked).Must().BeNull();

      static bool IsTheReplyToTheShellsEnd(string reply) => Regex.IsMatch(reply, @"^The background shell b[a-z0-9]{8} ended\.$");
   }
}
