using Cloyd.Chat.Status;
using Cloyd.Hub;
using Cloyd.Hub.RunningSessions;
using Cloyd.Sessions;

namespace Cloyd.ProjectsDashboard;

/// <summary>The name and activity of one host, including its shell after the agent exits, or one read-only session outside a host.</summary>
sealed class DashboardSession(DashboardProject group) : DashboardRow
{
   public DashboardProject Group { get; } = group;
   public SessionHostOfTheHub? Host { get; private set; }
   public RunningSession? Running { get; private set; }
   public Session? Latest { get; private set; }
   public bool HasHost => Host is not null;
   public double Opacity => HasHost ? 1 : 0.6;
   public string Name { get => field; private set => SetProperty(ref field, value); } = "";
   public string Activity { get => field; private set => SetProperty(ref field, value); } = "";
   /// <summary>The <see cref="RunningSession"/> whose activity mark the row shows during a turn.</summary>
   public RunningSession? WorkingSession { get => field; private set => SetProperty(ref field, value); }

   public void Update(SessionHostOfTheHub? host, RunningSession? running, Session? latest)
   {
      Host = host;
      Running = running;
      Latest = running?.Session ?? latest ?? Latest;
      Name = Latest?.Name ?? Latest?.AgentCli.TitleOfASessionWithNoName ?? "Shell";
      WorkingSession = running is { IsBusy: true } ? running : null;
      IsWaiting = running is RunningClaudeCode { Status.IsWaiting: true } or RunningCodex { Status.IsWaiting: true };
      Activity = running switch
      {
         RunningClaudeCode { Status.IsWaiting: true } it => it.Status.WaitingFor ?? "Waiting for you",
         RunningClaudeCode { TurnUnderWay: {} turn } => $"{turn.Activity.AsShown} {TimeTaken.Since(turn.StartedAt)}",
         RunningClaudeCode it => it.BackgroundWorkCounted ?? it.Status.AsReported,
         RunningCodex it => it.Status.WaitingFor ?? it.Status.AsReported,
         _ => Latest is null ? "No agent running" : "Shell"
      };
   }
}
