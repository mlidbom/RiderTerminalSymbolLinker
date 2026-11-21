using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Cloyd.Chat.Views;
using Cloyd.Hub.RunningSessions;

namespace Cloyd.Chat.Status;

/// <summary>
/// The lines under the chat's input box: Claude Code's own status line, as the terminal shows it under Claude Code's box,
/// in its colours, such as "Opus 5.5 · xhigh · context 42% · week 100% · Fable 7% · €361.07" with "week 100%" in bold
/// red; the permission mode under it, such as "⏵⏵ bypass permissions on"; and under that, while background work runs,
/// what runs, counted as Claude Code counts it beside its box, such as "1 shell", with each task in its tip. Magnus,
/// 2026-11-21: "get all the information from our custom status line in the CLI into the status line here", and "add the
/// missing information about background work under the existing two lines under the input box".
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

   readonly TextBlock _claudeCodesStatusLine = Grey();
   readonly TextBlock _permissionMode = Grey();
   readonly TextBlock _backgroundWork = Grey();

   public StatusLineOfTheChat()
   {
      Resources.MergedDictionaries.Add(ColoursOfTheTerminal.AsResources());
      Content = new StackPanel { Margin = new Thickness(28, 0, 28, 8), Children = { _claudeCodesStatusLine, _permissionMode, _backgroundWork } };
      Show(claudeCode: null);
   }

   /// <summary>What it shows of Claude Code's own status line: empty until the hub knows it, and <see cref="NoClaudeCodeRuns"/> while none runs.</summary>
   public string ClaudeCodesStatusLine => LinesOfTheChat.TextIn(_claudeCodesStatusLine);

   /// <summary>The runs it shows Claude Code's own status line in, each stretch of it in its style.</summary>
   public IReadOnlyList<Run> RunsOfClaudeCodesStatusLine => [.. _claudeCodesStatusLine.Inlines!.OfType<Run>()];

   /// <summary>The permission mode under it, as Claude Code shows it, such as "⏵⏵ bypass permissions on": null while it shows none.</summary>
   public string? PermissionMode => _permissionMode.IsVisible ? _permissionMode.Text : null;

   /// <summary>The line under the permission mode while background work runs, its <see cref="RunningClaudeCode.BackgroundWorkCounted"/>, such as "1 shell": null while none runs.</summary>
   public string? BackgroundWork => _backgroundWork.IsVisible ? _backgroundWork.Text : null;

   /// <summary>The tip of the line of the background work: each task and scheduled prompt with what it does (<see cref="Hub.RunningSessions.BackgroundWork.Listed"/>), null while the hub knows not what runs.</summary>
   public string? TipOfTheBackgroundWork => ToolTip.GetTip(_backgroundWork) as string;

   /// <summary>Shows what <paramref name="claudeCode"/>, the Claude Code that runs in the tab, showed last under its box, and what background work runs: null while none runs.</summary>
   public void Show(RunningClaudeCode? claudeCode)
   {
      _claudeCodesStatusLine.Inlines!.Clear();
      if(claudeCode is null) _claudeCodesStatusLine.Inlines.Add(new Run(NoClaudeCodeRuns));
      foreach(var stretch in claudeCode?.StatusLine?.Stretches ?? []) _claudeCodesStatusLine.Inlines.Add(RunOf(stretch));
      _permissionMode.Text = claudeCode?.StatusLine?.PermissionModeLine;
      _permissionMode.IsVisible = _permissionMode.Text is not null;
      _backgroundWork.Text = claudeCode?.BackgroundWorkCounted;
      _backgroundWork.IsVisible = _backgroundWork.Text is not null;
      ToolTip.SetTip(_backgroundWork, claudeCode?.BackgroundWork is { IsAny: true } work ? work.Listed : null);
   }

   /// <summary><paramref name="stretch"/> as a run of text in its style: its colour's shade (<see cref="ColoursOfTheTerminal"/>), else the line's grey.</summary>
   static Run RunOf(StretchOfTheStatusLine stretch)
   {
      var run = new Run(stretch.Text) { FontWeight = stretch.IsBold ? FontWeight.Bold : FontWeight.Normal };
      return stretch.Colour is {} name && ColoursOfTheTerminal.Named(name) is {} colour ? colour.AsTheForegroundOf(run) : run;
   }

   static TextBlock Grey() => ColoursOfTheChat.Grey.AsTheForegroundOf(new TextBlock { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Left, TextTrimming = TextTrimming.CharacterEllipsis });
}
