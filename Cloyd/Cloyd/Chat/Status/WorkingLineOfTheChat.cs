using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Cloyd.Chat.Views;
using Cloyd.Hub;

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
/// load the <c>tooling-commands</c> mod, from when the line saw Claude Code turn busy.
/// </remarks>
sealed class WorkingLineOfTheChat : UserControl
{
   /// <summary>What the line says Claude does while the hub tells of no turn under way.</summary>
   public const string Working = "Working…";

   static readonly IBrush ClaudesOrange = new SolidColorBrush(Color.Parse("#D97757"));

   readonly TextBlock _says = new() { FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };

   /// <summary>Counts the time the turn has taken, a second at a time.</summary>
   readonly DispatcherTimer _ticking = new() { Interval = TimeSpan.FromSeconds(1) };

   RunningClaudeCode? _claudeCode;
   ToolCallsOfTheLastTurn _callsOfTheTurn = new([]);

   /// <summary>When the line saw Claude Code turn busy: null while it isn't.</summary>
   DateTime? _busySince;

   public WorkingLineOfTheChat()
   {
      _ticking.Tick += (_, _) => Update();
      IsVisible = false;
      Content = new DockPanel { Margin = new Thickness(28, 2, 28, 2), Children = { Docked(Spinner), _says } };
   }

   /// <summary>The spinner at the start of the line, which turns while the line shows.</summary>
   public SpinnerOfClaudeCode Spinner { get; } = new();

   /// <summary>What the line says, as Claude Code's own spinner line, such as <c>Thinking… (12s · ↓ 1.2k tokens)</c>: null while it doesn't show.</summary>
   public string? Says => IsVisible ? LinesOfTheChat.TextIn(_says) : null;

   /// <summary>Shows what <paramref name="claudeCode"/>, the Claude Code that runs in the tab, does: the line shows while it is busy. Null while none runs.</summary>
   public void Show(RunningClaudeCode? claudeCode)
   {
      _claudeCode = claudeCode;
      var isBusy = claudeCode?.Status.IsBusy == true;
      if(isBusy && _busySince is null) _busySince = DateTime.UtcNow;
      if(!isBusy) _busySince = null;
      _ticking.IsEnabled = isBusy;
      Spinner.IsTurning = isBusy;
      Update();
   }

   /// <summary>Shows what the conversation, <paramref name="conversation"/> as the chat shows it, has of the turn: the commands it ran.</summary>
   public void Show(IReadOnlyList<ShownInTheChat> conversation)
   {
      _callsOfTheTurn = ToolCallsOfTheLastTurn.In(conversation);
      Update();
   }

   void Update()
   {
      IsVisible = _busySince is not null;
      if(_busySince is not {} busySince) return;

      var turn = _claudeCode?.TurnUnderWay;
      _says.Inlines!.Clear();
      _says.Inlines.Add(new Run(turn?.Activity.AsShown ?? Working) { FontWeight = FontWeight.SemiBold, Foreground = ClaudesOrange });
      _says.Inlines.Add(ColoursOfTheChat.Grey.AsTheForegroundOf(new Run($" ({string.Join(" · ", HowTheTurnGoes())})")));

      IEnumerable<string> HowTheTurnGoes()
      {
         yield return TimeTaken.Since(turn?.StartedAt ?? busySince).ToString();
         if(turn?.OutputTokensSoFar is long tokens and > 0) yield return $"↓ {InThousands(tokens)} tokens";
         if(_callsOfTheTurn.Commands is var commands and > 0) yield return commands == 1 ? "1 command" : $"{commands} commands";
      }
   }

   /// <summary><paramref name="tokens"/>, as Claude Code's spinner counts them: <c>228</c>, <c>1.2k</c>.</summary>
   static string InThousands(long tokens) => tokens < 1000 ? $"{tokens}" : (tokens / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + "k";

   static Control Docked(Control control)
   {
      DockPanel.SetDock(control, Dock.Left);
      return control;
   }
}
