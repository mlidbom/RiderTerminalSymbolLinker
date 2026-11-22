using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Media;
using Cloyd.Chat.Views;

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
   readonly TextBlock _whatItDoes = new() { FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis };
   readonly TextBlock _whatItWasGiven = new() { FontFamily = LinesOfTheChat.Monospaced, FontSize = 12, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(4, 1, 0, 0) };

   public CallUnderWayOfTheChat(ClaudeCodeTurnStatus status)
   {
      ColoursOfTheChat.Text.AsTheForegroundOf(_whatItDoes);
      ColoursOfTheChat.Grey.AsTheForegroundOf(_whatItWasGiven);
      var description = new Run { FontWeight = FontWeight.SemiBold };
      var duration = ColoursOfTheChat.Grey.AsTheForegroundOf(new Run());
      description.Bind(Run.TextProperty, CompiledBinding.Create((ClaudeCodeTurnStatus it) => it.CallDescription, status));
      duration.Bind(Run.TextProperty, CompiledBinding.Create((ClaudeCodeTurnStatus it) => it.CallTime, status));
      _whatItDoes.Inlines!.Add(description);
      _whatItDoes.Inlines.Add(duration);
      _whatItWasGiven.Bind(TextBlock.TextProperty, CompiledBinding.Create((ClaudeCodeTurnStatus it) => it.CallInput, status));
      _whatItWasGiven.Bind(IsVisibleProperty, CompiledBinding.Create((ClaudeCodeTurnStatus it) => it.HasCallInput, status));
      Bind(IsVisibleProperty, CompiledBinding.Create((ClaudeCodeTurnStatus it) => it.HasCall, status));
      Content = new StackPanel { Margin = new Thickness(28, 2, 28, 4), Children = { _whatItDoes, _whatItWasGiven } };
      Loaded += (_, _) => status.RowLoaded();
      Unloaded += (_, _) => status.RowUnloaded();
   }

   /// <summary>The call's description and elapsed time, as drawn.</summary>
   public string? WhatItDoes => IsVisible ? LinesOfTheChat.TextIn(_whatItDoes) : null;

   /// <summary>The preview of the current call's input.</summary>
   public string? WhatItWasGiven => IsVisible && _whatItWasGiven.IsVisible ? _whatItWasGiven.Text : null;
}
