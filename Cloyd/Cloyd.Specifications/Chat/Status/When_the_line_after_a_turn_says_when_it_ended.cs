using System.Globalization;
using Cloyd.Chat.Status;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Status;

/// <summary>
/// The line after a turn says when the turn ended, "done 14:37", as Claude Code's TUI does: by the time alone today, else
/// with the weekday or the date, by the days of the calendar rather than the hours since. In the machine's culture, which
/// the facts expect each part in. Said on a Saturday afternoon, 2026-11-21 at 15:00.
/// </summary>
public class When_the_line_after_a_turn_says_when_it_ended
{
   static readonly DateTime Now = new(2026, 11, 21, 15, 0, 0);

   [XF] public void a_turn_that_ended_today_by_its_time_alone() => SaidOf(new DateTime(2026, 11, 21, 14, 37, 0)).Must().Be(TimeOf(new DateTime(2026, 11, 21, 14, 37, 0)));

   [XF] public void one_that_ended_yesterday_by_its_weekday_and_time_however_few_hours_ago()
   {
      var lateLastNight = new DateTime(2026, 11, 20, 23, 50, 0);
      new WhenItEnded(lateLastNight).AsSaidOn(new DateTime(2026, 11, 21, 0, 10, 0)).Must().Be($"{WeekdayOf(lateLastNight)} {TimeOf(lateLastNight)}");
   }

   [XF] public void one_that_ended_six_days_ago_by_its_weekday_and_time() =>
      SaidOf(new DateTime(2026, 11, 15, 9, 5, 0)).Must().Be($"{WeekdayOf(new DateTime(2026, 11, 15))} {TimeOf(new DateTime(2026, 11, 15, 9, 5, 0))}");

   [XF] public void one_that_ended_a_week_ago_or_more_by_its_date_and_time() =>
      SaidOf(new DateTime(2026, 11, 14, 9, 5, 0)).Must().Be($"{new DateTime(2026, 11, 14).ToString("d", CultureInfo.CurrentCulture)} {TimeOf(new DateTime(2026, 11, 14, 9, 5, 0))}");

   static string SaidOf(DateTime ended) => new WhenItEnded(ended).AsSaidOn(Now);

   static string TimeOf(DateTime time) => time.ToString("t", CultureInfo.CurrentCulture);

   static string WeekdayOf(DateTime day) => day.ToString("dddd", CultureInfo.CurrentCulture);
}
