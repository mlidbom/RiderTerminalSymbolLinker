namespace Cloyd.ProjectsDashboard.Filters;

sealed record ActivityChoice(SessionActivityFilter Filter, string Label)
{
   public static IReadOnlyList<ActivityChoice> Choices { get; } =
   [
      new(SessionActivityFilter.All, "All statuses"), new(SessionActivityFilter.Running, "Running"),
      new(SessionActivityFilter.ReadyToResume, "Ready to resume"), new(SessionActivityFilter.Working, "Working"),
      new(SessionActivityFilter.Waiting, "Waiting for you"), new(SessionActivityFilter.Idle, "Idle"), new(SessionActivityFilter.Error, "Error")
   ];
   public override string ToString() => Label;
}
