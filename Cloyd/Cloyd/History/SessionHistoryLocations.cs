using Cloyd.Hub;
using Cloyd.Hub.RunningSessions;
using Cloyd.Projects;
using Cloyd.Sessions;

namespace Cloyd.History;

/// <summary>Where the sessions in one history reading can be shown. A currently running agent takes
/// precedence over an older shell that last ran the same session.</summary>
sealed class SessionHistoryLocations
{
   readonly Dictionary<SessionId, WhereTheSessionIs> _locations = [];

   public SessionHistoryLocations(ProjectWindows windows, IReadOnlyList<RunningSession> running, IReadOnlyList<SessionHostOfTheHub> hosts)
   {
      var hostsById = hosts.ToDictionary(it => it.Id);
      var occupied = running.Select(it => it.SessionHost).ToHashSet();
      var tabs = windows.Open.SelectMany(window => window.Tabs.Select(tab => (Window: window, Tab: tab))).ToArray();
      var tabsByHost = tabs.DistinctBy(it => it.Tab.Host.Id).ToDictionary(it => it.Tab.Host.Id);
      var dashboard = windows.Dashboard?.Tree.Sessions.Where(it => it.Host is not null).ToDictionary(it => it.Host!.Id) ?? [];

      foreach(var (window, tab) in tabs)
         if(tab.LatestSession is {} latest && hostsById.ContainsKey(tab.Host.Id) && !occupied.Contains(tab.Host.Id))
            _locations.TryAdd(latest.Id, new WhereTheSessionIs.ExitedInATab(window, tab));
      foreach(var (id, row) in dashboard)
         if(row.Latest is {} latest && hostsById.TryGetValue(id, out var host) && !occupied.Contains(id))
            _locations.TryAdd(latest.Id, new WhereTheSessionIs.InProjectsDashboard(host, running: false));
      foreach(var agent in running.DistinctBy(it => it.Session.Id)) _locations[agent.Session.Id] = LocationOf(agent);

      WhereTheSessionIs LocationOf(RunningSession agent)
      {
         if(agent.SessionHost is not {} id || !hostsById.TryGetValue(id, out var host)) return new WhereTheSessionIs.RunningOutsideASessionHost();
         if(tabsByHost.TryGetValue(id, out var tab)) return new WhereTheSessionIs.RunningInATab(tab.Window, tab.Tab);
         if(dashboard.ContainsKey(id)) return new WhereTheSessionIs.InProjectsDashboard(host, running: true);
         return new WhereTheSessionIs.RunningInAHostNoTabShows(host);
      }
   }

   public WhereTheSessionIs Of(SessionId session) => _locations.GetValueOrDefault(session, WhereTheSessionIs.NotRunning.Anywhere);
}
