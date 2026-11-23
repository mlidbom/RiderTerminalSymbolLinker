using System.Text.Json;
using Cloyd.ClaudeCode;
using Cloyd.Codex;
using Cloyd.History;
using Cloyd.Hub;
using Cloyd.Hub.RunningSessions;
using Cloyd.Projects;
using Cloyd.Projects.Git;
using Cloyd.ProjectsDashboard;
using Cloyd.ProjectsDashboard.Filters;
using Cloyd.Saving;
using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

public class When_dashboard_filter_switches_overlap
{
   [XF] public void running_includes_every_live_agent_while_specific_statuses_add_independently()
   {
      var settings = new SavedDashboardArrangement { ToolbarFilters = IncludedSessions.Running };
      var filters = new DashboardSessionFilters(settings);
      var idleWithShell = RowOf(new RunningClaudeCode(new ClaudeCodeProcessId(1), new ClaudeCodeSessionId("claude"), null, null,
                                                       new StatusOfAClaudeCode("shell", null), null, null, null));
      var busy = CodexWith("busy");
      var waiting = CodexWith("waiting");
      var error = CodexWith("error");
      var otherActivity = CodexWith("a-new-status");
      var ready = new DashboardSession("recorded");
      var recorded = new SessionOnDisk(new CodexSession(new CodexSessionId("stopped"), null), null, null, null, null, DateTime.UtcNow, false, true);
      ready.Update(null, null, recorded.Session, recorded.Session.AgentCli, null, SessionListing.FromDisk(recorded));
      var shell = new DashboardSession("a shell");
      DashboardSession[] rows = [idleWithShell, busy, waiting, error, otherActivity, ready, shell];
      rows.Where(filters.Includes).Must().SequenceEqual([idleWithShell, busy, waiting, error, otherActivity]);

      settings.ToolbarFilters = IncludedSessions.Waiting | IncludedSessions.ReadyToResume;
      rows.Where(filters.Includes).Must().SequenceEqual([waiting, ready]);
      settings.ToolbarFilters = IncludedSessions.Idle | IncludedSessions.Working | IncludedSessions.Error;
      rows.Where(filters.Includes).Must().SequenceEqual([idleWithShell, busy, error]);
      filters.Toggle(null, IncludedSessions.All);
      rows.Where(filters.Includes).Must().SequenceEqual(rows);
      filters.Toggle(null, IncludedSessions.All);
      rows.Where(filters.Includes).Must().SequenceEqual([idleWithShell, busy, error]);

      ready.ReadPin([ready.PinKey], new HashSet<SessionId>());
      settings.ToolbarFilters = IncludedSessions.Pinned | IncludedSessions.Waiting;
      rows.Where(filters.Includes).Must().SequenceEqual([waiting, ready]);

      static DashboardSession CodexWith(string status) =>
         RowOf(new RunningCodex(new CodexSessionId(status), null, null, new StatusOfACodex(status)));
      static DashboardSession RowOf(RunningSession agent)
      {
         var row = new DashboardSession(agent.Session.Id.Value);
         row.Update(null, agent, agent.Session, agent.Session.AgentCli, null);
         return row;
      }
   }

   [XF] public void a_persisted_zero_for_either_strip_takes_precedence_over_legacy_settings_in_any_property_order()
   {
      var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
      foreach(var saved in new[]
      {
         """{"rootStatuses":0,"rootInclusions":0,"onlyPinned":true,"activityFilter":6}""",
         """{"onlyPinned":true,"activityFilter":6,"rootInclusions":0,"rootStatuses":0}"""
      })
      {
         var settings = JsonSerializer.Deserialize<SavedDashboardArrangement>(saved, json)!;
         settings.ToolbarFilters.Must().Be(IncludedSessions.None);
         new DashboardSessionFilters(settings).Effective(null).Must().Be(IncludedSessions.All);
         var reopened = JsonSerializer.Deserialize<SavedDashboardArrangement>(JsonSerializer.Serialize(settings, json), json)!;
         reopened.ToolbarFilters.Must().Be(IncludedSessions.None);
      }
   }

   [XF] public void old_All_memberships_in_a_worktree_still_honours_the_restrictive_toolbar_status()
   {
      var group = new DashboardWorktree(new GitWorktree(new Project(@"C:\Topic"), "topic", false));
      var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
      var oldSettings = new { rootInclusions = 2, rootStatuses = 64, groupFilters = new Dictionary<string, object>
         { [group.Key] = new { specified = 512, included = 512 } } };
      var settings = JsonSerializer.Deserialize<SavedDashboardArrangement>(JsonSerializer.Serialize(oldSettings), json)!;
      var filters = new DashboardSessionFilters(settings);
      filters.Effective(null).Must().Be(IncludedSessions.Pinned | IncludedSessions.Waiting);
      filters.Effective(group).Must().Be(IncludedSessions.Waiting);
   }

   [XF] public void the_old_unfiltered_default_opens_on_All_and_one_click_selects_only_Waiting()
   {
      var settings = JsonSerializer.Deserialize<SavedDashboardArrangement>("""{"rootInclusions":5,"rootStatuses":1024}""",
                                                                          new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
      var filters = new DashboardSessionFilters(settings);
      filters.Effective(null).Must().Be(IncludedSessions.All);
      filters.Toggle(null, IncludedSessions.Waiting);
      filters.Effective(null).Must().Be(IncludedSessions.Waiting);
   }

   [XF] public void clicking_an_individual_worktree_switch_under_automatic_All_turns_off_only_that_worktrees_All()
   {
      var parent = new DashboardProject(new Project(@"C:\Main"));
      var worktree = new DashboardWorktree(new GitWorktree(new Project(@"C:\Topic"), "topic", false)) { Parent = parent };
      var settings = new SavedDashboardArrangement { ToolbarFilters = IncludedSessions.None };
      var filters = new DashboardSessionFilters(settings);
      filters.AutomaticallyIncludesAll(worktree).Must().BeTrue();

      filters.Toggle(worktree, IncludedSessions.Pinned);
      filters.LocalSetting(worktree, IncludedSessions.Pinned).Must().Be(true);
      filters.LocalSetting(worktree, IncludedSessions.All).Must().Be(false);
      filters.LocalSetting(worktree, IncludedSessions.OutsideTabs).Must().BeNull();

      var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
      var reopened = new DashboardSessionFilters(JsonSerializer.Deserialize<SavedDashboardArrangement>(JsonSerializer.Serialize(settings, json), json)!);
      reopened.Toggle(null, IncludedSessions.OpenTabs);
      reopened.Toggle(null, IncludedSessions.All);
      reopened.Effective(parent).HasFlag(IncludedSessions.All).Must().BeTrue();
      reopened.Effective(worktree).HasFlag(IncludedSessions.All).Must().BeFalse();
      reopened.Effective(worktree).HasFlag(IncludedSessions.OpenTabs | IncludedSessions.Pinned).Must().BeTrue();
   }
}
