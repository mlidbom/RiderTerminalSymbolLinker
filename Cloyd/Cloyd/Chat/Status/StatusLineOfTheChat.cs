using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Cloyd.Hub;

namespace Cloyd.Chat.Status;

/// <summary>
/// The lines under the chat's input box: Claude Code's own status line, as the terminal shows it under Claude Code's box,
/// such as "Opus 5.5 · xhigh · context 42% · week 100% · Fable 7% · €361.07"; the permission mode under it, such as
/// "⏵⏵ bypass permissions on"; and under that, while Claude is idle and background work runs, a line that says so.
/// Magnus, 2026-11-21: "get all the information from our custom status line in the CLI into the status line here", and
/// "add the missing information about background work under the existing two lines under the input box".
/// </summary>
/// <remarks>
/// What Claude does otherwise isn't told here: the <see cref="WorkingLineOfTheChat"/> over the box tells that it works, and
/// <see cref="WaitingForMagnusInTheTerminal"/> that it waits for him in the terminal. Magnus, of "Idle" here: "This is not
/// needed with progress clearly shown above the input."
/// </remarks>
sealed class StatusLineOfTheChat : UserControl
{
   /// <summary>What the line tells while no Claude Code runs in the tab, as before it starts, or after it exits to the shell.</summary>
   public const string NoClaudeCodeRuns = "No Claude Code runs in this tab";

   /// <summary>
   /// What the line under the permission mode says while Claude is idle and background work runs. The hub tells neither how
   /// many tasks nor which, as Claude Code's own "1 shell" does: see <see cref="StatusOfAClaudeCode.IsIdleWithBackgroundWork"/>.
   /// </summary>
   public const string BackgroundWorkRuns = "A background shell or monitor runs";

   readonly TextBlock _claudeCodesStatusLine = Grey();
   readonly TextBlock _permissionMode = Grey();
   readonly TextBlock _backgroundWork = Grey();

   public StatusLineOfTheChat()
   {
      _backgroundWork.Text = BackgroundWorkRuns;
      Content = new StackPanel { Margin = new Thickness(28, 0, 28, 8), Children = { _claudeCodesStatusLine, _permissionMode, _backgroundWork } };
      Show(claudeCode: null);
   }

   /// <summary>What it shows of Claude Code's own status line: empty until the hub knows it, and <see cref="NoClaudeCodeRuns"/> while none runs.</summary>
   public string ClaudeCodesStatusLine => _claudeCodesStatusLine.Text!;

   /// <summary>The permission mode under it, as Claude Code shows it, such as "⏵⏵ bypass permissions on": null while it shows none.</summary>
   public string? PermissionMode => _permissionMode.IsVisible ? _permissionMode.Text : null;

   /// <summary>The line under the permission mode, <see cref="BackgroundWorkRuns"/>, while Claude is idle and background work runs: null otherwise.</summary>
   public string? BackgroundWork => _backgroundWork.IsVisible ? _backgroundWork.Text : null;

   /// <summary>Shows what <paramref name="claudeCode"/>, the Claude Code that runs in the tab, showed last under its box, and whether background work runs: null while none runs.</summary>
   public void Show(RunningSession? claudeCode)
   {
      _claudeCodesStatusLine.Text = claudeCode is null ? NoClaudeCodeRuns : claudeCode.StatusLine?.Text ?? "";
      _permissionMode.Text = claudeCode?.StatusLine?.PermissionModeLine;
      _permissionMode.IsVisible = _permissionMode.Text is not null;
      _backgroundWork.IsVisible = claudeCode?.Status.IsIdleWithBackgroundWork == true;
   }

   static TextBlock Grey() => new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Left, TextTrimming = TextTrimming.CharacterEllipsis, Opacity = 0.7 };
}
