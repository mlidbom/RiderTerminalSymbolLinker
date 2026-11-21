namespace Cloyd.Chat.Status;

/// <summary>How long a turn or a tool call has taken so far, or took, as Claude Code's own UI says it: <c>42s</c>, <c>3m 05s</c>.</summary>
readonly record struct TimeTaken(TimeSpan Taken)
{
   /// <summary>The time taken since <paramref name="startedAt"/>, in UTC, until now.</summary>
   public static TimeTaken Since(DateTime startedAt) => new(DateTime.UtcNow - startedAt);

   public override string ToString() => Taken.TotalMinutes < 1 ? $"{Math.Max(0, (int)Taken.TotalSeconds)}s" : $"{(int)Taken.TotalMinutes}m {Taken.Seconds:00}s";
}
