using Cloyd.Codex;
using Cloyd.History;
using Cloyd.Hub;
using Cloyd.Hub.RunningSessions;
using Cloyd.Projects;
using Compze.Must;
using Compze.xUnitBDD;
using Xunit;

namespace Cloyd.Specifications.History;

public class When_windows_share_the_session_catalogue
{
   [XF] public async Task closing_one_picker_leaves_the_shared_read_available_to_another_window()
   {
      using var closed = new CancellationTokenSource();
      var disk = new TaskCompletionSource<IReadOnlyList<SessionOnDisk>>(TaskCreationOptions.RunContinuationsAsynchronously);
      var reads = 0;
      var catalogue = new SessionCatalogue(_ => { ++reads; return disk.Task; }, CancellationToken.None);
      var first = catalogue.Read(closed.Token);
      var second = catalogue.Read(CancellationToken.None, force: true);
      closed.Cancel();
      await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
      disk.SetResult([]);
      await second;
      reads.Must().Be(1);
      catalogue.HasRead.Must().BeTrue();
   }

   [XF] public async Task a_response_from_the_previous_window_lifetime_is_never_published()
   {
      using var lifetime = new CancellationTokenSource();
      var disk = new TaskCompletionSource<IReadOnlyList<SessionOnDisk>>(TaskCreationOptions.RunContinuationsAsynchronously);
      var catalogue = new SessionCatalogue(_ => disk.Task, lifetime.Token);
      var changes = 0;
      catalogue.Changed += () => ++changes;
      var reading = catalogue.Read(CancellationToken.None);
      lifetime.Cancel();
      disk.SetResult([]);
      await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading);
      changes.Must().Be(0);
      catalogue.HasRead.Must().BeFalse();
   }

   [XF] public async Task a_new_live_session_and_a_renamed_recorded_session_each_appear_once_with_their_current_folders()
   {
      var recorded = new CodexSession(new CodexSessionId("recorded"), "Earlier name");
      var onDisk = new SessionOnDisk(recorded, null, "Earlier prompt", @"C:\OldFolder", "old-branch", DateTime.UtcNow, false, true);
      var catalogue = new SessionCatalogue(_ => Task.FromResult<IReadOnlyList<SessionOnDisk>>([onDisk]), CancellationToken.None);
      await catalogue.Read(CancellationToken.None);
      var live = new RunningCodex(recorded.Id, "Current name", null, new StatusOfACodex("idle")) { WorkingFolder = new Project(@"C:\CurrentFolder") };
      var newSession = new RunningCodex(new CodexSessionId("new"), null, null, new StatusOfACodex("idle"));
      var records = catalogue.WithRunning([live, newSession]);
      records.Count.Must().Be(2);
      records[recorded.Id].Title.Must().Be("Current name");
      records[recorded.Id].Prompt.Must().Be("Earlier prompt");
      records[recorded.Id].Directory.Must().Be(@"C:\CurrentFolder");
      records[recorded.Id].Branch.Must().BeNull();
      records[newSession.SessionId].Recorded.Must().BeNull();
   }
}
