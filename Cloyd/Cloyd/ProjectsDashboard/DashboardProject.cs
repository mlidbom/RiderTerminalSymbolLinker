using System.Collections.ObjectModel;
using Cloyd.Projects;

namespace Cloyd.ProjectsDashboard;

/// <summary>A project in Projects Dashboard, or the separate group of sessions running outside a host.</summary>
sealed class DashboardProject(Project? project) : DashboardRow
{
   public Project? Project { get; } = project;
   public string Name => Project is null ? "Outside session hosts" : Project.Name.Length > 0 ? Project.Name : Project.Folder;
   public bool CanStartSession => Project is not null;
   public ObservableCollection<DashboardSession> Sessions { get; } = [];
   public void UpdateWaiting() => IsWaiting = Sessions.Any(it => it.IsWaiting);
}
