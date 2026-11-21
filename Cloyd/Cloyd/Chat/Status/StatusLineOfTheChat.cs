using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Cloyd.Hub;

namespace Cloyd.Chat.Status;

/// <summary>
/// The line under the chat's input box, as the desktop app's footer has the model and the modes: at its left, whether
/// Claude waits for Magnus in the terminal, or is idle, while the working line over the box tells that it works; at its
/// right, Claude Code's own status line, the
/// model, the effort level, the permission mode in short and the context used, such as "Opus 5.5 · xhigh · Auto · 23%
/// context". Magnus, 2026-11-20: "The stuff below it should come from the current status line shown in the TUI".
/// </summary>
sealed class StatusLineOfTheChat : UserControl
{
   /// <summary>What the line tells while no Claude Code runs in the tab, as before it starts, or after it exits to the shell.</summary>
   public const string NoClaudeCodeRuns = "No Claude Code runs in this tab";

   readonly TextBlock _whatClaudeDoes = Grey(HorizontalAlignment.Left);
   readonly TextBlock _claudeCodesStatusLine = Grey(HorizontalAlignment.Right);

   public StatusLineOfTheChat()
   {
      DockPanel.SetDock(_claudeCodesStatusLine, Dock.Right);
      Content = new DockPanel { Margin = new Thickness(28, 0, 28, 8), Children = { _claudeCodesStatusLine, _whatClaudeDoes } };
      Show(claudeCode: null);
   }

   /// <summary>What it tells of what Claude does, such as "Idle": empty while Claude works.</summary>
   public string WhatClaudeDoes => _whatClaudeDoes.Text!;

   /// <summary>What it shows of Claude Code's own status line: empty until the hub knows it.</summary>
   public string ClaudeCodesStatusLine => _claudeCodesStatusLine.Text!;

   /// <summary>
   /// Shows what <paramref name="claudeCode"/>, the Claude Code that runs in the tab, does and showed last: null while none
   /// runs. Nothing of what Claude does while it works: the <see cref="WorkingLineOfTheChat"/> over the box tells it, as
   /// Magnus asked (2026-11-21).
   /// </summary>
   public void Show(RunningSession? claudeCode)
   {
      _whatClaudeDoes.Text = claudeCode is null ? NoClaudeCodeRuns : claudeCode.Status.IsBusy ? "" : claudeCode.Status.AsShown;
      _claudeCodesStatusLine.Text = claudeCode?.StatusLine?.AsShown ?? "";
   }

   static TextBlock Grey(HorizontalAlignment alignment) => new() { FontSize = 12, HorizontalAlignment = alignment, TextTrimming = TextTrimming.CharacterEllipsis, Opacity = 0.7 };
}
