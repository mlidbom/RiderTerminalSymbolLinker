using Cloyd.Chat.ToolCalls;

namespace Cloyd.Chat;

partial record ShownInTheChat
{
   /// <summary>
   /// Tool calls Claude made one after another, with nothing shown between them: folded into one grey line that counts
   /// them, such as <c>Read 2 files, ran 2 commands, 1 failed</c>, which opens to a row for each. A call by itself is its
   /// own line instead.
   /// </summary>
   public sealed record RunOfToolCalls(IReadOnlyList<ToolCallInTheChat> Calls) : ShownInTheChat
   {
      /// <summary>What tells the run apart as the conversation goes on: the id of its first call, which the calls added after it leave as it is.</summary>
      public string Id => Calls[0].Id;

      /// <summary>Whether it is one call by itself, which is shown as its own line, with no line counting it.</summary>
      public bool IsOneCall => Calls.Count == 1;

      /// <summary>The run, with <paramref name="call"/> made after its calls.</summary>
      public RunOfToolCalls With(ToolCallInTheChat call) => new([.. Calls, call]);

      /// <summary>
      /// The line that counts the calls, in the past tense, kind by kind in the order Claude first made each, and how many
      /// failed or were stopped: <c>Edited a file, created a file, ran a command</c>.
      /// </summary>
      public string Counted
      {
         get
         {
            var parts = Calls.GroupBy(it => it.Kind).Select(it => it.Key.Counted(it.Count())).ToList();
            if(HowManyEnded(OutcomeOfAToolCall.Failed) is var failed and > 0) parts.Add($"{failed} failed");
            if(HowManyEnded(OutcomeOfAToolCall.Stopped) is var stopped and > 0) parts.Add($"{stopped} stopped");
            var line = string.Join(", ", parts);
            return char.ToUpperInvariant(line[0]) + line[1..];
         }
      }

      int HowManyEnded(OutcomeOfAToolCall outcome) => Calls.Count(it => it.Outcome == outcome);

      /// <summary>How many lines the run's edits added, as its line shows in green, such as <c>+9</c>: 0 when it made none.</summary>
      public int LinesAdded => Calls.Where(it => it.Outcome == OutcomeOfAToolCall.Done).Sum(it => it.LinesAdded);

      public bool Equals(RunOfToolCalls? other) => other is not null && Calls.SequenceEqual(other.Calls);

      public override int GetHashCode() => HashCode.Combine(Id, Calls.Count);
   }
}
