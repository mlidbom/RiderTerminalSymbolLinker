using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Cloyd.Chat.ToolCalls;
using Cloyd.Chat.Views;
using Cloyd.Hub;

namespace Cloyd.Chat.Status;

/// <summary>
/// The lines over the chat's input box that show, while Claude works, that it does, and at what, as Claude Code's own UI
/// shows them: the tool call under way, if one is, as what it does and how long it has run, such as
/// <c>Finding the repositories that use it · 5s</c>, with what it was given under it, such as <c>└ $ ls /c/Dev</c>; then the
/// turn's line: a spinner (<see cref="SpinnerOfClaudeCode"/>), what the <see cref="TurnUnderWay"/> does, how long the
/// turn has taken, its output tokens and how many commands it has run, such as
/// <c>Thinking… (12m 05s · ↓ 1.2k tokens · 3 commands)</c>. They take no room while Claude doesn't work.
/// </summary>
/// <remarks>
/// Magnus, 2026-11-20: "The lack of any information about that in the new UI is what keeps me switching back to the TUI
/// constantly at the moment, to see what is going on." And 2026-11-21: "put it above the input, like other claude UIs do
/// and include the time and tokens like they do"; then, of the call and the turn in one line: "I would like it separated
/// like in the CLI". Over the box, rather than at the conversation's end, so that they show however the conversation is
/// scrolled, as the TUI's do.<br/>
/// The turn's time is counted from its start, or, while the hub tells of no turn under way, as in a session that doesn't
/// load the <c>tooling-commands</c> mod, from when the line saw Claude Code turn busy. The call's from when Claude made
/// it, as the conversation has it.
/// </remarks>
sealed class WorkingLineOfTheChat : UserControl
{
   /// <summary>What the turn's line says Claude does while the hub tells of no turn under way.</summary>
   public const string Working = "Working…";

   /// <summary>How many lines of what a call was given show under it: what is longer is cut, with an ellipsis, as the TUI cuts it.</summary>
   const int LinesShownOfWhatACallWasGiven = 3;

   static readonly IBrush ClaudesOrange = new SolidColorBrush(Color.Parse("#D97757"));

   readonly TextBlock _callUnderWay = new() { FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis, IsVisible = false };
   readonly TextBlock _whatTheCallWasGiven = new() { FontFamily = LinesOfTheChat.Monospaced, FontSize = 12, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(4, 1, 0, 6), IsVisible = false };
   readonly TextBlock _says = new() { FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };

   /// <summary>Counts the time the turn and the call under way have taken, a second at a time.</summary>
   readonly DispatcherTimer _ticking = new() { Interval = TimeSpan.FromSeconds(1) };

   RunningSession? _claudeCode;
   ToolCallsOfTheLastTurn _callsOfTheTurn = new([]);

   /// <summary>When the line saw Claude Code turn busy: null while it isn't.</summary>
   DateTime? _busySince;

   public WorkingLineOfTheChat()
   {
      ColoursOfTheChat.Text.AsTheForegroundOf(_callUnderWay);
      ColoursOfTheChat.Grey.AsTheForegroundOf(_whatTheCallWasGiven);
      _ticking.Tick += (_, _) => Update();
      IsVisible = false;
      Content = new StackPanel
      {
         Margin = new Thickness(28, 2, 28, 2),
         Children = { _callUnderWay, _whatTheCallWasGiven, new DockPanel { Children = { Docked(Spinner), _says } } }
      };
   }

   /// <summary>The spinner at the start of the turn's line, which turns while the lines show.</summary>
   public SpinnerOfClaudeCode Spinner { get; } = new();

   /// <summary>What the turn's line says, as Claude Code's own spinner line, such as <c>Thinking… (12s · ↓ 1.2k tokens)</c>: null while it doesn't show.</summary>
   public string? Says => IsVisible ? TextOf(_says) : null;

   /// <summary>The tool call under way, as what it does and how long it has run, such as <c>Finding the repositories that use it · 5s</c>: null while none shows.</summary>
   public string? CallUnderWay => IsVisible && _callUnderWay.IsVisible ? TextOf(_callUnderWay) : null;

   /// <summary>What the tool call under way was given, as it shows under the call, such as <c>└ $ ls /c/Dev</c>: null while it shows none.</summary>
   public string? WhatTheCallUnderWayWasGiven => IsVisible && _whatTheCallWasGiven.IsVisible ? _whatTheCallWasGiven.Text : null;

   /// <summary>Shows what <paramref name="claudeCode"/>, the Claude Code that runs in the tab, does: the lines show while it is busy. Null while none runs.</summary>
   public void Show(RunningSession? claudeCode)
   {
      _claudeCode = claudeCode;
      var isBusy = claudeCode?.Status.IsBusy == true;
      if(isBusy && _busySince is null) _busySince = DateTime.UtcNow;
      if(!isBusy) _busySince = null;
      _ticking.IsEnabled = isBusy;
      Spinner.IsTurning = isBusy;
      Update();
   }

   /// <summary>Shows what the conversation, <paramref name="conversation"/> as the chat shows it, has of the turn: its tool calls.</summary>
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
      ShowTheCall(turn is null || turn.Activity.IsRunningTools ? _callsOfTheTurn.UnderWay : null);
      _says.Inlines!.Clear();
      _says.Inlines.Add(new Run(turn?.Activity.AsShown ?? Working) { FontWeight = FontWeight.SemiBold, Foreground = ClaudesOrange });
      _says.Inlines.Add(ColoursOfTheChat.Grey.AsTheForegroundOf(new Run($" ({string.Join(" · ", HowTheTurnGoes())})")));

      IEnumerable<string> HowTheTurnGoes()
      {
         yield return HowLong(DateTime.UtcNow - (turn?.StartedAt ?? busySince));
         if(turn?.OutputTokensSoFar is long tokens and > 0) yield return $"↓ {InThousands(tokens)} tokens";
         if(_callsOfTheTurn.Commands is var commands and > 0) yield return commands == 1 ? "1 command" : $"{commands} commands";
      }
   }

   /// <summary>Shows <paramref name="call"/>, the tool call under way, over the turn's line: none while it is null.</summary>
   void ShowTheCall(ToolCallInTheChat? call)
   {
      _callUnderWay.IsVisible = call is not null;
      _callUnderWay.Inlines!.Clear();
      if(call is not null)
      {
         _callUnderWay.Inlines.Add(new Run(call.WhatItDoes) { FontWeight = FontWeight.SemiBold });
         _callUnderWay.Inlines.Add(ColoursOfTheChat.Grey.AsTheForegroundOf(new Run($" · {HowLong(DateTime.UtcNow - call.MadeAt)}")));
      }

      _whatTheCallWasGiven.Text = call?.WhatItWasGiven is {} given ? AsShownUnderTheCall(given) : null;
      _whatTheCallWasGiven.IsVisible = _whatTheCallWasGiven.Text is not null;
   }

   /// <summary>
   /// <paramref name="given"/> as the TUI shows what a call was given under it: its first lines, the first after <c>└</c>,
   /// the others in line with it, and an ellipsis after the last shown when more follow.
   /// </summary>
   static string AsShownUnderTheCall(string given)
   {
      var lines = given.Split('\n');
      var shown = lines.Take(LinesShownOfWhatACallWasGiven).Select((line, at) => (at == 0 ? "└ " : "  ") + line).ToList();
      if(lines.Length > LinesShownOfWhatACallWasGiven) shown[^1] += "…";
      return string.Join("\n", shown);
   }

   static string TextOf(TextBlock block) => block.Inlines!.Aggregate("", (said, it) => said + ((Run)it).Text);

   /// <summary><paramref name="taken"/>, as Claude Code's own UI says how long a turn has taken: <c>42s</c>, <c>3m 05s</c>.</summary>
   static string HowLong(TimeSpan taken) => taken.TotalMinutes < 1 ? $"{Math.Max(0, (int)taken.TotalSeconds)}s" : $"{(int)taken.TotalMinutes}m {taken.Seconds:00}s";

   /// <summary><paramref name="tokens"/>, as Claude Code's spinner counts them: <c>228</c>, <c>1.2k</c>.</summary>
   static string InThousands(long tokens) => tokens < 1000 ? $"{tokens}" : (tokens / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + "k";

   static Control Docked(Control control)
   {
      DockPanel.SetDock(control, Dock.Left);
      return control;
   }
}
