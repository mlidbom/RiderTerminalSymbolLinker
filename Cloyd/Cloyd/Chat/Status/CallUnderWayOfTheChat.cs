using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using Cloyd.Chat.ToolCalls;
using Cloyd.Chat.Views;
using Cloyd.Hub;

namespace Cloyd.Chat.Status;

/// <summary>
/// The tool call under way while Claude works, over the chat's input box, as Claude Code's own UI shows a call that runs:
/// what it does and how long it has run, such as <c>Finding the repositories that use it · 5s</c>, with what it was given
/// under it, such as <c>└ $ ls /c/Dev</c>. The <see cref="WorkingLineOfTheChat"/> shows under it, and the messages queued
/// behind it between the two. It takes no room while no call is under way.
/// </summary>
/// <remarks>
/// Magnus, 2026-11-21: "so that I can see what the agent is doing right now"; and of the call and the turn in one line:
/// "I would like it separated like in the CLI".<br/>
/// The call is the last of the turn's with no result yet (<see cref="ToolCallsOfTheLastTurn"/>), from what the chat shows
/// of the conversation: shown while the turn runs tools, and always while Claude works where the hub tells of no turn, as
/// in a session that doesn't load the <c>tooling-commands</c> mod. Its time counts from when Claude made it.
/// </remarks>
sealed class CallUnderWayOfTheChat : UserControl
{
   /// <summary>How many lines of what a call was given show under it: what is longer is cut, with an ellipsis, as the TUI cuts it.</summary>
   const int LinesShownOfWhatTheCallWasGiven = 3;

   readonly TextBlock _whatItDoes = new() { FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis };
   readonly TextBlock _whatItWasGiven = new() { FontFamily = LinesOfTheChat.Monospaced, FontSize = 12, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(4, 1, 0, 0), IsVisible = false };

   /// <summary>Counts the time the call has run, a second at a time.</summary>
   readonly DispatcherTimer _ticking = new() { Interval = TimeSpan.FromSeconds(1) };

   RunningSession? _claudeCode;
   ToolCallsOfTheLastTurn _callsOfTheTurn = new([]);

   public CallUnderWayOfTheChat()
   {
      ColoursOfTheChat.Text.AsTheForegroundOf(_whatItDoes);
      ColoursOfTheChat.Grey.AsTheForegroundOf(_whatItWasGiven);
      _ticking.Tick += (_, _) => Update();
      IsVisible = false;
      Content = new StackPanel { Margin = new Thickness(28, 2, 28, 4), Children = { _whatItDoes, _whatItWasGiven } };
   }

   /// <summary>The call under way, as what it does and how long it has run, such as <c>Finding the repositories that use it · 5s</c>: null while none shows.</summary>
   public string? WhatItDoes => IsVisible ? LinesOfTheChat.TextIn(_whatItDoes) : null;

   /// <summary>What the call under way was given, as it shows under the call, such as <c>└ $ ls /c/Dev</c>: null while it shows none.</summary>
   public string? WhatItWasGiven => IsVisible && _whatItWasGiven.IsVisible ? _whatItWasGiven.Text : null;

   /// <summary>Shows the call <paramref name="claudeCode"/>, the Claude Code that runs in the tab, runs, while it is busy. Null while none runs.</summary>
   public void Show(RunningSession? claudeCode)
   {
      _claudeCode = claudeCode;
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
      var call = CallShown();
      IsVisible = call is not null;
      _ticking.IsEnabled = IsVisible;
      _whatItDoes.Inlines!.Clear();
      if(call is null) return;

      _whatItDoes.Inlines.Add(new Run(call.WhatItDoes) { FontWeight = FontWeight.SemiBold });
      _whatItDoes.Inlines.Add(ColoursOfTheChat.Grey.AsTheForegroundOf(new Run($" · {TimeTaken.Since(call.MadeAt)}")));
      _whatItWasGiven.Text = call.WhatItWasGiven is {} given ? AsShownUnderTheCall(given) : null;
      _whatItWasGiven.IsVisible = _whatItWasGiven.Text is not null;

      ToolCallInTheChat? CallShown()
      {
         if(_claudeCode is not { Status.IsBusy: true } claudeCode) return null;
         return claudeCode.TurnUnderWay is not {} turn || turn.Activity.IsRunningTools ? _callsOfTheTurn.UnderWay : null;
      }
   }

   /// <summary>
   /// <paramref name="given"/> as the TUI shows what a call was given under it: its first lines, the first after <c>└</c>,
   /// the others in line with it, and an ellipsis after the last shown when more follow.
   /// </summary>
   static string AsShownUnderTheCall(string given)
   {
      var lines = given.Split('\n');
      var shown = lines.Take(LinesShownOfWhatTheCallWasGiven).Select((line, at) => (at == 0 ? "└ " : "  ") + line).ToList();
      if(lines.Length > LinesShownOfWhatTheCallWasGiven) shown[^1] += "…";
      return string.Join("\n", shown);
   }
}
