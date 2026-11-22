using System.Collections.ObjectModel;
using Cloyd.Projects;

namespace Cloyd.ProjectsDashboard;

/// <summary>Every session the hub lists, grouped by its project even when its project window is closed.</summary>
sealed class DashboardTree(ProjectWindows windows)
{
   public ObservableCollection<DashboardProject> Projects { get; } = [];
   public IEnumerable<DashboardSession> Sessions => Projects.SelectMany(it => it.Sessions);

   public void Refresh()
   {
      var sessions = windows.Sessions;
      var live = new HashSet<DashboardSession>();
      var groups = windows.Open.Select(it => Group(it.Project)).ToHashSet();
      foreach(var host in sessions.Hosts)
      {
         var group = Group(host.Project ?? new Project(host.Directory));
         groups.Add(group);
         var row = group.Sessions.FirstOrDefault(it => it.Host?.Id == host.Id) ?? new DashboardSession(group);
         var running = sessions.Running.FirstOrDefault(it => it.SessionHost == host.Id);
         var latest = sessions.Known(host.Id)?.LatestSession ?? row.Latest;
         if(latest is null && row.Host is null) latest = sessions.Saved(host)?.Session;
         row.Update(host, running, latest);
         Keep(row);
      }
      foreach(var running in sessions.Running.Where(it => it.SessionHost is null))
      {
         var group = Group(null);
         groups.Add(group);
         var row = group.Sessions.FirstOrDefault(it => it.Latest?.Id == running.Session.Id) ?? new DashboardSession(group);
         row.Update(null, running, running.Session);
         Keep(row);
      }

      foreach(var group in Projects.ToArray())
      {
         foreach(var gone in group.Sessions.Where(it => !live.Contains(it)).ToArray()) group.Sessions.Remove(gone);
         if(!groups.Contains(group)) Projects.Remove(group);
         else group.UpdateWaiting();
      }

      void Keep(DashboardSession row)
      {
         if(!row.Group.Sessions.Contains(row)) row.Group.Sessions.Add(row);
         live.Add(row);
      }
   }

   DashboardProject Group(Project? project)
   {
      if(Projects.FirstOrDefault(it => Equals(it.Project, project)) is {} existing) return existing;
      var group = new DashboardProject(project);
      var index = Projects.TakeWhile(it => it.Project is not null && (project is null || string.Compare(it.Name, group.Name, StringComparison.OrdinalIgnoreCase) <= 0)).Count();
      Projects.Insert(index, group);
      return group;
   }
}
