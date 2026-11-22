using CommunityToolkit.Mvvm.ComponentModel;

namespace Cloyd.ProjectsDashboard;

/// <summary>A project or session in the tree. Rows keep their identity across hub updates, so selection and expansion stay put.</summary>
abstract class DashboardRow : ObservableObject
{
   public bool IsWaiting { get => field; protected set => SetProperty(ref field, value); }
}
