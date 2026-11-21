using Avalonia.Controls;
using Avalonia.Media;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Chat.Status;
using Cloyd.Chat.Views;
using Cloyd.ClaudeCode;
using Cloyd.Hub;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Status;

/// <summary>
/// The line under a chat's input box, shown a Claude Code whose status line, as the machine's status line command makes
/// it, the hub knows, in its styles, and its permission mode under it: all of it, as the terminal shows it (Magnus,
/// 2026-11-21: "get all the information from our custom status line in the CLI into the status line here"). Made here,
/// for a status line of the machine's, which the stand-in for Claude Code's isn't.
/// </summary>
public class When_a_chats_status_line_shows_Claude_Codes_own : SpecificationOpeningWindows
{
   static readonly ClaudeCodesStatusLine TheMachinesStatusLine = new(
      "Opus 5.5 · xhigh · context 42% · week 100% · Fable 7% · €361.07 · 72% · home 1 · .claude 24",
      [new("Opus 5.5 · xhigh · context 42% · ", null, false), new("week 100%", "red", true), new(" · Fable 7% · ", null, false), new("€361.07", "yellow", false), new(" · ", null, false), new("72%", "yellow", false), new(" · home 1 · .claude 24", null, false)],
      "bypass permissions on");

   readonly StatusLineOfTheChat _statusLine;

   public When_a_chats_status_line_shows_Claude_Codes_own() => _statusLine = Once(() =>
   {
      var window = Open(() => new Window { Width = 840, Height = 80, Content = new StatusLineOfTheChat(), Resources = { MergedDictionaries = { ColoursOfTheChat.AsResources() } } });
      var statusLine = OnTheUIThread(() => (StatusLineOfTheChat)window.Content!);
      OnTheUIThread(() => statusLine.Show(AClaudeCode(TheMachinesStatusLine, status: "shell")));
      Render(window, "a-chats-status-line");
      return statusLine;
   });

   [XF] public void it_shows_the_status_line_as_the_terminal_does() => OnTheUIThread(() => _statusLine.ClaudeCodesStatusLine).Must().Be(TheMachinesStatusLine.Text);

   [XF] public void each_stretch_in_its_style_as_the_week_nearly_used_up_in_bold_red() =>
      OnTheUIThread(() => _statusLine.RunsOfClaudeCodesStatusLine.Single(it => it.Text == "week 100%") is var week
                          && week.FontWeight == FontWeight.Bold
                          && week.Foreground is ISolidColorBrush { Color: var colour }
                          && colour == ColoursOfTheTerminal.Named("red")!.In(_statusLine.ActualThemeVariant))
        .Must().BeTrue();

   [XF] public void a_stretch_in_the_default_style_in_the_lines_grey() =>
      OnTheUIThread(() => _statusLine.RunsOfClaudeCodesStatusLine.First() is { Foreground: ISolidColorBrush { Color: var colour } } first
                          && first.FontWeight == FontWeight.Normal
                          && colour == ColoursOfTheChat.Grey.In(_statusLine.ActualThemeVariant))
        .Must().BeTrue();

   [XF] public void under_it_the_permission_mode_as_Claude_Code_shows_it() => OnTheUIThread(() => _statusLine.PermissionMode).Must().Be("⏵⏵ bypass permissions on");

   /// <summary>
   /// The Claude Code is idle with a background shell task or monitor running, as the hub's <c>shell</c> status tells, and
   /// the hub knows not what, as until a turn has ended since it started. Magnus, 2026-11-21: "add the missing information
   /// about background work under the existing two lines under the input box". What it shows once the hub knows what runs
   /// is in <see cref="When_a_chats_status_line_shows_what_runs_in_the_background"/>.
   /// </summary>
   [XF] public void under_that_that_background_work_runs_while_the_hub_knows_not_what() => OnTheUIThread(() => (_statusLine.BackgroundWork, _statusLine.TipOfTheBackgroundWork)).Must().Be((BackgroundWork.SomeRuns, (string?)null));

   [XF] public void with_no_background_work_running_it_shows_no_line_of_it() =>
      OnTheUIThread(() =>
      {
         var statusLine = new StatusLineOfTheChat();
         statusLine.Show(AClaudeCode(TheMachinesStatusLine, status: "idle"));
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
      new(new ClaudeCodeProcessId(4711), new ClaudeCodeSessionId(Guid.NewGuid().ToString()), "a session", new SessionHostId(Guid.NewGuid().ToString()), new StatusOfAClaudeCode(status, WaitingFor: null), statusLine, TurnUnderWay: null, BackgroundWork: null);
}
