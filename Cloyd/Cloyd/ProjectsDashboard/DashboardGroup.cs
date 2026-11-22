using Cloyd.Projects;

namespace Cloyd.ProjectsDashboard;

/// <summary>A collapsible project or worktree. A collapsed group's summary includes all of its session tabs.</summary>
abstract class DashboardGroup(string key) : DashboardRow(key)
{
   public List<DashboardRow> Children { get; } = [];
   public abstract string Name { get; }
   public abstract string Description { get; }
   public abstract Project? StartFolder { get; }
   public bool CanStartSession => StartFolder is not null;
   public bool IsWorktree => this is DashboardWorktree;
   public string Disclosure => Expanded ? "▾" : "▸";
   public bool Expanded
   {
      get => field;
      set { if(SetProperty(ref field, value)) OnPropertyChanged(nameof(Disclosure)); }
   } = true;
   public string Summary { get => field; private set => SetProperty(ref field, value); } = "";
   public IEnumerable<DashboardSession> Sessions => Children.OfType<DashboardSession>().Concat(Children.OfType<DashboardGroup>().SelectMany(it => it.Sessions));

   public void Summarize()
   {
      var sessions = Sessions.ToArray();
      var waiting = sessions.Count(it => it.IsWaiting);
      var busy = sessions.Count(it => it.Heading?.Running?.IsBusy == true);
      IsWaiting = waiting > 0;
      Summary = Expanded && !IsWaiting ? "" : string.Join(" · ", Details());

      IEnumerable<string> Details()
      {
         if(!Expanded) yield return sessions.Length == 1 ? "1 session" : $"{sessions.Length} sessions";
         if(waiting > 0) yield return $"{waiting} waiting";
         if(!Expanded && busy > 0) yield return $"{busy} working";
      }
   }
}
