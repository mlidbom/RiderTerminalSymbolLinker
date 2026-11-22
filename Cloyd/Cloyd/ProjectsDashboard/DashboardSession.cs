using Cloyd.Chat.Status;
using Cloyd.Hub;
using Cloyd.Hub.RunningSessions;
using Cloyd.Sessions;

namespace Cloyd.ProjectsDashboard;

/// <summary>The name and activity of one host, including its shell after the agent exits, or one read-only session outside a host.</summary>
sealed class DashboardSession(string key) : DashboardRow(key)
{
   public SessionHostOfTheHub? Host { get; private set; }
   public RunningSession? Running { get; private set; }
   public Session? Latest { get; private set; }
   public bool HasHost => Host is not null;
   public double Opacity => HasHost ? 1 : 0.6;
   public string Name { get => field; private set => SetProperty(ref field, value); } = "";
   public string Activity { get => field; private set => SetProperty(ref field, value); } = "";
   public SessionTabHeading? Heading { get => field; private set => SetProperty(ref field, value); }
   public bool HasActivity => Activity.Length != 0;

   public void Update(SessionHostOfTheHub? host, RunningSession? running, Session? latest, AgentCli? knownAgent)
   {
      Host = host;
      Running = running;
      Latest = running?.Session ?? latest ?? Latest;
      Name = Latest?.Name ?? Latest?.AgentCli.TitleOfASessionWithNoName ?? "Shell";
      Heading = new SessionTabHeading(Name, Latest?.AgentCli ?? knownAgent, running);
      IsWaiting = running is RunningClaudeCode { Status.IsWaiting: true } or RunningCodex { Status.IsWaiting: true };
      Activity = running switch
      {
         RunningClaudeCode { Status.IsWaiting: true } it => it.Status.WaitingFor ?? "Waiting for you",
         RunningClaudeCode { TurnUnderWay: {} turn } => $"{turn.Activity.AsShown} {TimeTaken.Since(turn.StartedAt)}",
         RunningClaudeCode it => it.BackgroundWorkCounted ?? (it.IsBusy ? "Working" : ""),
         RunningCodex { Status.IsWaiting: true } it => it.Status.WaitingFor ?? "Waiting for you",
         RunningCodex it when it.IsBusy => "Working",
         RunningCodex { Status.AsReported: "error" } => "Error",
         _ => ""
      };
      OnPropertyChanged(nameof(HasActivity));
   }
}
