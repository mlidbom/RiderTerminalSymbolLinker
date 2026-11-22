using CommunityToolkit.Mvvm.ComponentModel;

namespace Cloyd.ProjectsDashboard;

/// <summary>A project or session in the tree. Rows keep their identity across hub updates, so selection and expansion stay put.</summary>
abstract class DashboardRow(string key) : ObservableObject
{
   public string Key { get; } = key;
   public DashboardGroup? Parent
   {
      get => field;
      set { if(SetProperty(ref field, value)) OnPropertyChanged(nameof(Group)); }
   }
   public DashboardProject Group => this as DashboardProject ?? Parent!.Group;
   public bool IsActive { get => field; set => SetProperty(ref field, value); }
   public bool IsWaiting { get => field; protected set => SetProperty(ref field, value); }
   public bool BelongsTo(DashboardGroup group) => Parent == group || Parent?.BelongsTo(group) == true;
}
