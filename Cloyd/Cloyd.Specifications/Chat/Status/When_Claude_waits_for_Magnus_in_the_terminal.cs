using Avalonia.Controls;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Chat.Status;
using Cloyd.ClaudeCode;
using Cloyd.Hub;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Status;

/// <summary>
/// The box between a chat's conversation and its input box, shown a Claude Code that waits for Magnus in a permission
/// prompt, which only its terminal shows: made here, as the stand-in for Claude Code shows no dialog. Magnus,
/// 2026-11-21, of the status line telling it alone: "Make it much more prominent than that. I could miss that for hours."
/// </summary>
public class When_Claude_waits_for_Magnus_in_the_terminal : SpecificationOpeningWindows
{
   readonly WaitingForMagnusInTheTerminal _box;
   readonly bool _theButtonAskedForTheTerminal;

   public When_Claude_waits_for_Magnus_in_the_terminal() => (_box, _theButtonAskedForTheTerminal) = Once(() =>
   {
      var window = Open(() => new Window { Width = 840, Height = 120, Content = new WaitingForMagnusInTheTerminal() });
      var box = OnTheUIThread(() => (WaitingForMagnusInTheTerminal)window.Content!);
      var askedForTheTerminal = false;
      OnTheUIThread(() =>
      {
         box.AsksForTheTerminal += () => askedForTheTerminal = true;
         box.Show(AClaudeCode(new StatusOfAClaudeCode("waiting", "permission prompt")));
      });
      Render(window, "a-chats-box-while-claude-waits-in-the-terminal");
      Click(TheButtonSaying(window, WaitingForMagnusInTheTerminal.TextOfTheButton));
      return (box, askedForTheTerminal);
   });

   [XF] public void the_box_shows() => OnTheUIThread(() => _box.IsVisible).Must().BeTrue();

   [XF] public void it_says_what_Claude_waits_for_and_where_to_answer_it() => OnTheUIThread(() => _box.WhatFor).Must().Be("Permission prompt: answer it in the terminal.");

   [XF] public void its_button_asks_for_the_terminal() => _theButtonAskedForTheTerminal.Must().BeTrue();

   [XF] public void once_Claude_works_again_the_box_goes() =>
      OnTheUIThread(() =>
      {
         var box = new WaitingForMagnusInTheTerminal();
         box.Show(AClaudeCode(new StatusOfAClaudeCode("waiting", "permission prompt")));
         box.Show(AClaudeCode(new StatusOfAClaudeCode("busy", WaitingFor: null)));
         return box.IsVisible;
      }).Must().BeFalse();

   static RunningClaudeCode AClaudeCode(StatusOfAClaudeCode status) =>
      new(new ClaudeCodeProcessId(4711), new ClaudeCodeSessionId(Guid.NewGuid().ToString()), "a session", new SessionHostId(Guid.NewGuid().ToString()), status, StatusLine: null, TurnUnderWay: null, BackgroundWork: null);
}
