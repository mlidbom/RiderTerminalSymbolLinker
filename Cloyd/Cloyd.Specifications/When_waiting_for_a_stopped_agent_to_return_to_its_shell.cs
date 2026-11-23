using Cloyd.Hub.Worktrees;
using Cloyd.Projects;
using Cloyd.ProjectsDashboard;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

public class When_waiting_for_a_stopped_agent_to_return_to_its_shell
{
   readonly Project _folder = new(Path.GetTempPath());
   readonly OccupantTarget _first = new("host1", 100, "codex", "one", 101, DateTime.UtcNow);
   readonly OccupantTarget _second = new("host2", 200, "codex", "two", 201, DateTime.UtcNow);

   [XF] public void a_later_stop_gets_its_own_deadline_and_expiry_never_reauthorizes_an_already_stopped_process()
   {
      var clock = new ManualTime();
      var watch = new WorktreeStopFollowUp(clock);
      var stale = new WorktreeOccupant(_first, "idle", "stopAgent", null);
      var worktree = new WorktreeOccupants(_folder, [stale, stale with { Target = _second }], []);
      watch.Stopped(_folder, _first);
      clock.Advance(70);
      watch.Stopped(_folder, _second);
      clock.Advance(6);
      watch.StillWaiting([worktree]).Must().BeTrue();
      watch.IsUnloading(stale).Must().BeFalse();
      watch.IsUnloading(stale with { Target = _second }).Must().BeTrue();
      watch.ExitConfirmed(_first).Must().BeTrue();
      clock.Advance(70);
      watch.StillWaiting([worktree]).Must().BeFalse();
      watch.ExitConfirmed(_second).Must().BeTrue();
      watch.ExitConfirmed(_second with { AgentStartedAt = DateTime.UnixEpoch }).Must().BeFalse();
   }

   [XF] public void positive_ownership_elsewhere_ends_the_wait_for_a_surviving_shell_even_when_both_row_lists_are_empty()
   {
      var watch = new WorktreeStopFollowUp(TimeProvider.System);
      var empty = new WorktreeOccupants(_folder, [], []);
      var outside = empty with { VerifiedShellsElsewhere = [_first.HostId!] };
      watch.Stopped(_folder, _first);
      watch.StillWaiting([empty]).Must().BeTrue();
      watch.Observe([empty]).Matches(watch.Observe([outside])).Must().BeFalse();
      watch.StillWaiting([outside]).Must().BeFalse();
   }

   sealed class ManualTime : TimeProvider
   {
      long _seconds;
      public override long TimestampFrequency => 1;
      public override long GetTimestamp() => _seconds;
      public void Advance(int seconds) => _seconds += seconds;
   }
}
