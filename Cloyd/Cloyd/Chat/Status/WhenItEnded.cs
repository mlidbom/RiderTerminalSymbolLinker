using System.Globalization;

namespace Cloyd.Chat.Status;

/// <summary>
/// When a turn ended, as Claude Code's TUI says it in the line after the turn, "done 2:37 PM": the time, for a turn that
/// ended today; the weekday and the time, for one that ended in the last week; else the date and the time. In the machine's
/// culture, as Windows formats them: the TUI formats them in its own.
/// </summary>
/// <param name="Time">When the turn ended, in the machine's time zone.</param>
/// <remarks>review-naming: <c>WhenItEnded</c>.</remarks>
readonly record struct WhenItEnded(DateTime Time)
{
   /// <summary>When the turn ended, as the hub tells it: <paramref name="timeInUtc"/>.</summary>
   public static WhenItEnded AtUtc(DateTime timeInUtc) => new(timeInUtc.ToLocalTime());

   /// <summary>When the turn ended, as it is said on <paramref name="now"/>'s day.</summary>
   public string AsSaidOn(DateTime now) => (now.Date - Time.Date).Days switch
   {
      0 => TheTime,
      > 0 and < 7 => $"{Time.ToString("dddd", CultureInfo.CurrentCulture)} {TheTime}",
      _ => $"{Time.ToString("d", CultureInfo.CurrentCulture)} {TheTime}"
   };

   /// <summary>When the turn ended, as it is said today.</summary>
   public override string ToString() => AsSaidOn(DateTime.Now);

   string TheTime => Time.ToString("t", CultureInfo.CurrentCulture);
}
