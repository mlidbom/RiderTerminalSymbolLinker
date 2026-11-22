using System.Text.Json;
using Cloyd.ClaudeCode;
using Cloyd.Codex;
using Cloyd.History;
using Cloyd.Hub;
using Cloyd.Hub.RunningSessions;
using Cloyd.ProjectsDashboard;
using Cloyd.ProjectsDashboard.Filters;
using Cloyd.Saving;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

public class When_dashboard_filter_switches_overlap
{
   [XF] public void running_includes_every_live_agent_while_specific_statuses_add_independently()
   {
      var settings = new SavedDashboardArrangement { RootMemberships = IncludedSessions.AllMemberships, GlobalStatuses = IncludedSessions.Running };
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

      settings.GlobalStatuses = IncludedSessions.Waiting | IncludedSessions.ReadyToResume;
      rows.Where(filters.Includes).Must().SequenceEqual([waiting, ready]);
      settings.GlobalStatuses = IncludedSessions.Idle | IncludedSessions.Working | IncludedSessions.Error;
      rows.Where(filters.Includes).Must().SequenceEqual([idleWithShell, busy, error]);
      filters.Toggle(null, IncludedSessions.AllStatuses);
      rows.Where(filters.Includes).Must().SequenceEqual(rows);
      filters.Toggle(null, IncludedSessions.AllStatuses);
      rows.Where(filters.Includes).Must().SequenceEqual([idleWithShell, busy, error]);

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
         settings.RootMemberships.Must().Be(IncludedSessions.None);
         settings.GlobalStatuses.Must().Be(IncludedSessions.None);
         var reopened = JsonSerializer.Deserialize<SavedDashboardArrangement>(JsonSerializer.Serialize(settings, json), json)!;
         reopened.RootMemberships.Must().Be(IncludedSessions.None);
         reopened.GlobalStatuses.Must().Be(IncludedSessions.None);
      }
   }
}
