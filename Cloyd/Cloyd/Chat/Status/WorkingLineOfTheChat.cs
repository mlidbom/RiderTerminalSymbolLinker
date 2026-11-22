using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Cloyd.Chat.Views;
using Cloyd.Hub.RunningSessions;

namespace Cloyd.Chat.Status;

/// <summary>
/// The turn's line over the chat's input box while Claude works, as Claude Code's own spinner line: a spinner
/// (<see cref="SpinnerOfClaudeCode"/>), what the <see cref="TurnUnderWay"/> does, how long the turn has taken, its output
/// tokens and how many commands it has run, such as <c>Thinking… (12m 05s · ↓ 1.2k tokens · 3 commands)</c>. The
/// <see cref="CallUnderWayOfTheChat"/> shows over it, and the messages queued behind the call between the two. It takes
/// no room while Claude doesn't work.
/// </summary>
/// <remarks>
/// Magnus, 2026-11-20: "The lack of any information about that in the new UI is what keeps me switching back to the TUI
/// constantly at the moment, to see what is going on." And 2026-11-21: "put it above the input, like other claude UIs do
/// and include the time and tokens like they do". Over the box, rather than at the conversation's end, so that it shows
/// however the conversation is scrolled, as the TUI's does.<br/>
/// The turn's time is counted from its start, or, while the hub tells of no turn under way, as in a session that doesn't
/// load the <c>tooling-commands</c> mod, from when the shared <see cref="ClaudeCodeTurnStatus"/> first saw Claude Code turn busy.
/// </remarks>
sealed class WorkingLineOfTheChat : UserControl
{
   public const string Working = ClaudeCodeTurnStatus.Working;

   readonly TextBlock _says = new() { FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };

   public WorkingLineOfTheChat(ClaudeCodeTurnStatus status)
   {
      var activity = ColoursOfTheChat.ClaudeActivity.AsTheForegroundOf(new Run { FontWeight = FontWeight.SemiBold });
      var details = ColoursOfTheChat.Grey.AsTheForegroundOf(new Run());
      activity.Bind(Run.TextProperty, CompiledBinding.Create((ClaudeCodeTurnStatus it) => it.Activity, status));
      details.Bind(Run.TextProperty, CompiledBinding.Create((ClaudeCodeTurnStatus it) => it.TurnDetails, status));
      _says.Inlines!.Add(activity);
      _says.Inlines.Add(details);
      DockPanel.SetDock(Spinner, Dock.Left);
      Content = new DockPanel { Margin = new Thickness(28, 2, 28, 2), Children = { Spinner, _says } };
      Bind(IsVisibleProperty, CompiledBinding.Create((ClaudeCodeTurnStatus it) => it.IsBusy, status));
      Spinner.Bind(SpinnerOfClaudeCode.IsTurningProperty, CompiledBinding.Create((ClaudeCodeTurnStatus it) => it.IsBusy, status));
      Loaded += (_, _) => status.RowLoaded();
      Unloaded += (_, _) => status.RowUnloaded();
   }

   /// <summary>The spinner at the start of this row.</summary>
   public SpinnerOfClaudeCode Spinner { get; } = new();

   /// <summary>The text of this row as drawn, including its elapsed time.</summary>
   public string? Says => IsVisible ? LinesOfTheChat.TextIn(_says) : null;
}
