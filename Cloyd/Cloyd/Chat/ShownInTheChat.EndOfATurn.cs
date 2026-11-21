using ClaudeTooling.Hub.LocalApi.Contract.Conversations.Messages;
using Cloyd.Chat.Status;
using Cloyd.Hub.RunningSessions;

namespace Cloyd.Chat;

partial record ShownInTheChat
{
   /// <summary>
   /// One of Claude's turns ended: a grey line after its last message, as Claude Code's TUI writes one after each turn,
   /// <c>Worked for 21s · done 14:37</c>. After the conversation's last turn, it tells what that turn left running in the
   /// background, as the TUI does: <c>· 1 shell still running</c>. A turn that was interrupted has none.
   /// </summary>
   /// <remarks>
   /// The TUI says what Claude did with a verb of its whimsy, such as <c>Sautéed for 21s</c>: the chat says <c>Worked</c>.
   /// </remarks>
   /// <param name="Id">The message's id.</param>
   /// <param name="Took">How long the turn took, from its prompt to its end.</param>
   /// <param name="Ended">When it ended.</param>
   /// <param name="StillRunning">
   /// What it left running in the background, counted, such as <c>1 shell</c>: null after a turn that left nothing running,
   /// and after every turn but the conversation's last, which is all the hub tells of (<see cref="RunningClaudeCode.BackgroundWork"/>).
   /// </param>
   public sealed record EndOfATurn(string Id, TimeTaken Took, WhenItEnded Ended, string? StillRunning) : ShownInTheChat
   {
      public static EndOfATurn Of(MessageInTheAnswer.EndOfATurn end) => new(end.Id, new TimeTaken(TimeSpan.FromMilliseconds(end.TurnTookMs)), WhenItEnded.AtUtc(end.Time), StillRunning: null);

      /// <summary>
      /// The line of the conversation's last turn's end, which tells what <paramref name="work"/> runs of tasks: the
      /// background work as of that end, as the hub tells it.
      /// </summary>
      public EndOfATurn TheLastWith(BackgroundWork? work) => this with { StillRunning = work?.TasksCounted };

      public override string TextShown => Line;

      /// <summary>The line, as the TUI writes it.</summary>
      public string Line => $"Worked for {Took} · done {Ended}{(StillRunning is {} running ? $" · {running} still running" : "")}";
   }
}
