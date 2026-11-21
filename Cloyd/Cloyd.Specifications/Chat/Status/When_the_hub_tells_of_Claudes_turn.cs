using ClaudeTooling.Hub.LocalApi.Contract.TurnsOfRunningSessions;
using Cloyd.Chat;
using Cloyd.ClaudeCode;
using Cloyd.Chat.Status;
using Cloyd.Sessions;
using Cloyd.Specifications.Chat.Writing;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Status;

/// <summary>
/// While the stand-in for Claude Code that runs in a chat's tab is busy, the sandbox's hub is told what its turn does, as
/// the <c>tooling-commands</c> mod tells it of each Claude Code session's: here by the specification, as the stand-in
/// tells it nothing. The turn started 75 seconds ago, and Claude writes a <c>Write</c> call: the chat's working line says
/// so, over its input box.
/// </summary>
public class When_the_hub_tells_of_Claudes_turn : SpecificationWritingInAChat
{
   readonly string _turn = Guid.NewGuid().ToString();
   readonly long _startedAt = DateTimeOffset.UtcNow.AddSeconds(-75).ToUnixTimeMilliseconds();
   private protected readonly SessionTab _tab;
   readonly ClaudeCodeProcessId _claudeCode;

   public When_the_hub_tells_of_Claudes_turn() => (_tab, _claudeCode) = Once(() =>
   {
      var (window, tab) = AChatOfANewSessionIn(Sandbox.Project("the-project"));
      TypeAndSend(window, "Be busy for 120 seconds");
      WaitUntil(() => tab.Chat.InputBox.ShowsTheStopButton, "the Stop button showed, as Claude worked");
      var claudeCode = OnTheUIThread(() => tab.SessionRunningInIt!.Process);
      Report(claudeCode, Event("turnStarted"), Event("responseRequested", response: 0), Event("toolCallBegun", response: 0, block: 0, toolUseId: "toolu_01", toolName: "Write"));
      WaitUntil(() => tab.Chat.WorkingLine.Says?.StartsWith("Writing the Write call…", StringComparison.Ordinal) == true, "the working line said Claude wrote the Write call");
      return (tab, claudeCode);
   });

   string? WorkingLineSays => OnTheUIThread(() => _tab.Chat.WorkingLine.Says);

   [XF] public void the_working_line_says_Claude_writes_the_Write_call_how_long_the_turn_has_taken_and_its_output_tokens() =>
      WorkingLineSays.Must().Satisfy(it => it is not null && System.Text.RegularExpressions.Regex.IsMatch(it, @"^Writing the Write call… \(1m 1\ds · ↓ 1\.2k tokens\)$"));

   /// <summary>Then Claude writes text, in two reports, a quarter of a second apart: the chat shows it at the conversation's end as it comes.</summary>
   public class and_then_Claude_writes_text : When_the_hub_tells_of_Claudes_turn
   {
      public and_then_Claude_writes_text() => Once(() =>
      {
         Report(_claudeCode, Event("responseEnded", response: 0, stopReason: "tool_use"), Event("responseRequested", response: 1), Event("text", response: 1, block: 0, offset: 0, text: "It is Friday,"));
         Report(_claudeCode, Event("text", response: 1, block: 0, offset: 13, text: " 20 November."));
         WaitUntil(() => _tab.Chat.Shown.LastOrDefault() is ShownInTheChat.ClaudesText { Markdown: "It is Friday, 20 November." }, "the chat showed the text Claude wrote at its end");
         return true;
      });

      [XF] public void the_chat_shows_the_text_at_the_conversations_end() =>
         OnTheUIThread(() => _tab.Chat.Shown.Last()).Must().Satisfy(it => it is ShownInTheChat.ClaudesText { Markdown: "It is Friday, 20 November." });

      /// <summary>Within half a second: the hub sends a turn's activity at most every half second, and the text as it comes.</summary>
      [XF] public void the_working_line_says_Claude_writes() =>
         WaitUntil(() => _tab.Chat.WorkingLine.Says?.StartsWith("Writing… (1m", StringComparison.Ordinal) == true, "the working line said Claude wrote");

      /// <summary>Then the turn ends before the block did, as when Magnus interrupts it: the text goes, as Claude Code never writes it to the transcript.</summary>
      public class and_then_the_turn_is_interrupted : and_then_Claude_writes_text
      {
         public and_then_the_turn_is_interrupted() => Once(() =>
         {
            Report(_claudeCode, Event("turnEnded", reason: "aborted"));
            WaitUntil(() => _tab.Chat.Shown.OfType<ShownInTheChat.ClaudesText>().All(it => !it.Markdown.StartsWith("It is Friday", StringComparison.Ordinal)), "the text Claude wrote went");
            return true;
         });

         [XF] public void the_chat_shows_the_text_no_more() =>
            OnTheUIThread(() => _tab.Chat.Shown.OfType<ShownInTheChat.ClaudesText>().Select(it => it.Markdown).ToList()).Must().Satisfy(it => !it.Any(text => text.StartsWith("It is Friday", StringComparison.Ordinal)));

         [XF] public void while_the_stand_in_is_busy_still_the_working_line_says_Claude_works() =>
            WaitUntil(() => _tab.Chat.WorkingLine.Says?.StartsWith(WorkingLineOfTheChat.Working + " (", StringComparison.Ordinal) == true, "the working line said Claude worked");
      }
   }

   private protected void Report(ClaudeCodeProcessId claudeCode, params EventOfATurnInTheRequest[] events) =>
      Sandbox.HubOfTheSandbox.Report(new ReportOfATurnInTheRequest(_turn, _startedAt, OutputTokensOfEndedResponses: 1200, events), claudeCode);

   static EventOfATurnInTheRequest Event(string kind, int? response = null, int? block = null, int? offset = null, string? text = null, string? toolUseId = null, string? toolName = null, string? stopReason = null, string? reason = null) =>
      new(kind, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), response, block, offset, text, toolUseId, toolName, Characters: null, stopReason, reason);
}
