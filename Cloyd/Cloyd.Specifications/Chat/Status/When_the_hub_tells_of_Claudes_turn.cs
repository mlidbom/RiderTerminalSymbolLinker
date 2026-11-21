using System.Globalization;
using System.Text.RegularExpressions;
using Cloyd.Chat;
using Cloyd.Chat.Writing;
using Cloyd.Sessions;
using Cloyd.Specifications.Chat.Writing;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Status;

/// <summary>
/// Magnus has the stand-in for Claude Code in a chat's tab work through a turn of 10 seconds, which it tells the sandbox's
/// hub of as the <c>tooling-commands</c> mod tells it of each Claude Code session's turns: Claude waits for the model,
/// thinks, writes a call of PowerShell, runs it, waits for the model again, and writes its reply, "Heard: " and the
/// prompt. The chat is looked at all along (<see cref="TheTurnAsTheChatShowedIt"/>), until it shows the reply as Claude
/// writes it.
/// </summary>
public class When_the_hub_tells_of_Claudes_turn : SpecificationWritingInAChat
{
   const string ThePrompt = "Be busy for 10 seconds";
   const string TheReply = $"Heard: {ThePrompt}";

   private protected readonly SessionTab _tab;
   private protected readonly TheTurnAsTheChatShowedIt _turn;

   public When_the_hub_tells_of_Claudes_turn() => (_tab, _turn) = Once(() =>
   {
      var (window, tab) = AChatOfANewSessionIn(Sandbox.Project("the-project"));
      var turn = new TheTurnAsTheChatShowedIt(tab);
      TypeAndSend(window, ThePrompt);
      WaitUntil(() => turn.LookedAt() && turn.ClaudesTextAtTheEnd.Count > 0 && tab.Chat.WorkingLine.Says?.StartsWith("Writing… (", StringComparison.Ordinal) == true,
                "the working line said Claude wrote, and the chat showed what it wrote", turn.WhatItSaw);
      return (tab, turn);
   });

   /// <summary>
   /// Each of what Claude does for 15 % of the turn or more, as the stand-in tells: the hub tells a turn's activity at most
   /// every half second, so it may not tell of Claude waiting for the model the second time, for 4 % of the turn.
   /// </summary>
   [XF] public void the_working_line_says_what_Claude_does_as_it_does_it() =>
      _turn.WhatClaudeDid.Must().Satisfy(it => Holds(it, inOrder: ["Waiting for the model…", "Thinking…", "Writing the PowerShell call…", "Running tools…", "Writing…"]));

   [XF] public void and_how_long_the_turn_has_taken_its_output_tokens_and_the_commands_it_has_run() =>
      _turn.WorkingLineSaid[^1].Must().Satisfy(it => Regex.IsMatch(it, @"^Writing… \(\d+s · ↓ \d+ tokens · 1 command\)$"));

   [XF] public void the_chat_shows_Claudes_reply_at_the_conversations_end_as_Claude_writes_it() =>
      _turn.ClaudesTextAtTheEnd.Must().Satisfy(it => it.Count > 0 && it.All(text => text.Length < TheReply.Length && TheReply.StartsWith(text, StringComparison.Ordinal)));

   /// <summary>Then the turn ends, as Claude Code writes the reply to the transcript.</summary>
   public class and_then_the_turn_ends : When_the_hub_tells_of_Claudes_turn
   {
      readonly DateTime _seenToEnd;

      public and_then_the_turn_ends() => _seenToEnd = Once(() =>
      {
         WaitUntil(() => _turn.LookedAt() && _tab.Chat.WorkingLine.Says is null && _tab.Chat.Shown is [.., ShownInTheChat.ClaudesText, ShownInTheChat.EndOfATurn],
                   "the working line went, and the conversation had Claude's reply and the turn's end", _turn.WhatItSaw);
         return DateTime.Now;
      });

      /// <summary>As Claude Code's TUI writes a line after each turn: "✻ Sautéed for 21s · done 2:37 PM".</summary>
      [XF] public void after_the_reply_the_chat_tells_the_turn_ended() => OnTheUIThread(() => _tab.Chat.Shown[^2]).Must().Satisfy(it => it is ShownInTheChat.ClaudesText { Markdown: TheReply });

      [XF] public void in_a_line_that_says_how_long_it_took_and_when_it_ended() => TheTurnsEnd.Success.Must().BeTrue();

      [XF] public void how_long_it_took_the_10_seconds_Claude_was_busy() => TheTurnsEnd.Groups["took"].Value.Must().Satisfy(it => int.Parse(it, CultureInfo.InvariantCulture) is >= 10 and < 60);

      /// <summary>As the machine's clock tells the time, which the scenario read once it saw the turn end.</summary>
      [XF] public void and_when_it_ended_by_the_clock_today() =>
         TimeOnly.Parse(TheTurnsEnd.Groups["done"].Value, CultureInfo.CurrentCulture).Must().Satisfy(it => TimeOnly.FromDateTime(_seenToEnd) - it < TimeSpan.FromMinutes(2));

      Match TheTurnsEnd => Regex.Match(OnTheUIThread(() => ((ShownInTheChat.EndOfATurn)_tab.Chat.Shown[^1]).Line), @"^Worked for (?<took>\d+)s · done (?<done>[^·]+)$");

      [XF] public void the_reply_grew_as_Claude_wrote_it_until_it_was_whole() =>
         _turn.ClaudesTextAtTheEnd.Must().Satisfy(it => it.Count >= 2 && it[^1] == TheReply && it.Zip(it.Skip(1)).All(pair => pair.Second.Length > pair.First.Length && pair.Second.StartsWith(pair.First, StringComparison.Ordinal)));

      [XF] public void the_chat_shows_the_reply_once() => RepliesIn(_tab).Must().Satisfy(it => it.SequenceEqual([TheReply]));

      [XF] public void the_working_line_goes() => OnTheUIThread(() => _tab.Chat.WorkingLine.Says).Must().BeNull();
   }

   /// <summary>Then Magnus clicks Stop while Claude writes, and the turn ends cut short: Claude Code never writes the reply to the transcript.</summary>
   public class and_then_Magnus_stops_it : When_the_hub_tells_of_Claudes_turn
   {
      public and_then_Magnus_stops_it() => Once(() =>
      {
         Click(TheButtonWithTheTip(_tab, InputBoxOfTheChat.TipOfTheStopButton));
         WaitUntil(() => _tab.Chat.WorkingLine.Says is null && !_tab.Chat.Shown.OfType<ShownInTheChat.ClaudesText>().Any(), "the working line went, and the text Claude wrote went");
         return true;
      });

      [XF] public void the_text_Claude_wrote_goes_and_no_reply_comes() => RepliesIn(_tab).Must().BeEmpty();

      [XF] public void the_working_line_goes() => OnTheUIThread(() => _tab.Chat.WorkingLine.Says).Must().BeNull();
   }

   /// <summary>Whether <paramref name="said"/> holds each of <paramref name="inOrder"/>, in that order, with or without more between them.</summary>
   static bool Holds(IReadOnlyList<string> said, IReadOnlyList<string> inOrder)
   {
      var found = 0;
      foreach(var it in said)
         if(found < inOrder.Count && it == inOrder[found]) found++;
      return found == inOrder.Count;
   }
}
