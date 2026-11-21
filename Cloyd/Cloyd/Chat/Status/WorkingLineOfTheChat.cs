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
/// The line over the chat's input box that shows, while Claude works, that it does, and at what, as Claude Code's own
/// UI, the desktop app and claude.ai show it: a spinner; what the <see cref="TurnUnderWay"/> does, such as thinking or
/// writing, or the tool call under way, with what the call was given under it, such as <c>$ ls /c/Dev</c>; how long the
/// turn has taken; its output tokens; and how many commands it has run, such as <c>Thinking… (12s · ↓ 1.2k tokens)</c>.
/// It takes no room while Claude doesn't work.
/// </summary>
/// <remarks>
/// Magnus, 2026-11-20: "The lack of any information about that in the new UI is what keeps me switching back to the TUI
/// constantly at the moment, to see what is going on." And 2026-11-21: "put it above the input, like other claude UIs do
/// and include the time and tokens like they do". Over the box, rather than at the conversation's end, so that it shows
/// however the conversation is scrolled, as the TUI's does.<br/>
/// The time is counted from the turn's start, or, while the hub tells of no turn under way, as in a session that doesn't
/// load the <c>tooling-commands</c> mod, from when the line saw Claude Code turn busy.
/// </remarks>
sealed class WorkingLineOfTheChat : UserControl
{
   /// <summary>What the line says Claude does while it knows nothing more: no turn is told of, and no tool call is under way.</summary>
   public const string Working = "Working…";

   /// <summary>The glyphs of the spinner, as Claude Code's own: each shown in turn, then back.</summary>
   const string GlyphsOfTheSpinner = "·✢✳✶✻✽✻✶✳✢";

   static readonly IBrush ClaudesOrange = new SolidColorBrush(Color.Parse("#D97757"));

   readonly TextBlock _spinner = new() { Foreground = ClaudesOrange, FontSize = 14, Width = 18, VerticalAlignment = VerticalAlignment.Center };
   readonly TextBlock _says = new() { FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
   readonly TextBlock _callUnderWay = new() { FontFamily = LinesOfTheChat.Monospaced, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(18, 1, 0, 0), IsVisible = false };
   readonly DispatcherTimer _ticking = new() { Interval = TimeSpan.FromMilliseconds(120) };

   RunningSession? _claudeCode;
   ToolCallsOfTheLastTurn _callsOfTheTurn = new([]);

   /// <summary>When the line saw Claude Code turn busy: null while it isn't.</summary>
   DateTime? _busySince;

   int _glyph;

   public WorkingLineOfTheChat()
   {
      ColoursOfTheChat.Grey.AsTheForegroundOf(_callUnderWay);
      _ticking.Tick += (_, _) =>
      {
         _glyph = (_glyph + 1) % GlyphsOfTheSpinner.Length;
         Update();
      };
      IsVisible = false;
      Content = new StackPanel
      {
         Margin = new Thickness(28, 2, 28, 2),
         Children = { new DockPanel { Children = { Docked(_spinner), _says } }, _callUnderWay }
      };
   }

   /// <summary>What the line says, as Claude Code's own spinner line, such as <c>Thinking… (12s · ↓ 1.2k tokens)</c>: null while it doesn't show.</summary>
   public string? Says => IsVisible ? _says.Inlines!.Aggregate("", (said, it) => said + ((Run)it).Text) : null;

   /// <summary>What the tool call under way was given, as the line shows it under what it says, such as <c>$ ls /c/Dev</c>: null while it shows none.</summary>
   public string? CallUnderWay => IsVisible && _callUnderWay.IsVisible ? _callUnderWay.Text : null;

   /// <summary>Shows what <paramref name="claudeCode"/>, the Claude Code that runs in the tab, does: the line shows while it is busy. Null while none runs.</summary>
   public void Show(RunningSession? claudeCode)
   {
      _claudeCode = claudeCode;
      var isBusy = claudeCode?.Status.IsBusy == true;
      if(isBusy && _busySince is null)
      {
         _busySince = DateTime.UtcNow;
         _ticking.Start();
      }
      if(!isBusy)
      {
         _busySince = null;
         _ticking.Stop();
      }

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
      var callShown = turn is null || turn.Activity.IsRunningTools ? _callsOfTheTurn.UnderWay : null;
      _spinner.Text = GlyphsOfTheSpinner[_glyph].ToString();
      _says.Inlines!.Clear();
      _says.Inlines.Add(new Run(WhatItDoes()) { FontWeight = FontWeight.SemiBold, Foreground = ClaudesOrange });
      _says.Inlines.Add(ColoursOfTheChat.Grey.AsTheForegroundOf(new Run($" ({string.Join(" · ", HowItGoes())})")));
      _callUnderWay.Text = callShown?.WhatItWasGiven is {} given ? "└ " + given : null;
      _callUnderWay.IsVisible = _callUnderWay.Text is not null;

      string WhatItDoes() => callShown?.WhatItDoes ?? turn?.Activity.AsShown ?? Working;

      IEnumerable<string> HowItGoes()
      {
         yield return HowLong(DateTime.UtcNow - (turn?.StartedAt ?? busySince));
         if(turn?.OutputTokensSoFar is long tokens and > 0) yield return $"↓ {InThousands(tokens)} tokens";
         if(_callsOfTheTurn.Commands is var commands and > 0) yield return commands == 1 ? "1 command" : $"{commands} commands";
      }
   }

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
