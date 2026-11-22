using Cloyd.Chat.Status;
using Cloyd.Hub;
using Cloyd.Hub.RunningSessions;
using Cloyd.Sessions;
using Cloyd.Projects;

namespace Cloyd.ProjectsDashboard;

/// <summary>The name and activity of one host, including its shell after the agent exits, or one read-only session outside a host.</summary>
sealed class DashboardSession(string key) : DashboardRow(key)
{
   public SessionHostOfTheHub? Host { get; private set; }
   public RunningSession? Running { get; private set; }
   public Session? Latest { get; private set; }
   public Project? WorkingFolder { get; private set; }
   public bool HasHost => Host is not null;
   /// <summary>The agent's session ID survives moving between hosts. A shell can be pinned by host until its first session is known.</summary>
   public string PinKey => Latest is {} session ? $"session:{session.AgentCli.NameInTheHub}:{session.Id}" : Key;
   public bool IsPinned { get => field; private set { if(SetProperty(ref field, value)) OnPropertyChanged(nameof(PinAction)); } }
   public string PinAction => IsPinned ? "Unpin session" : "Pin session";
   /// <summary>A running session represents its pin when an older shell also remembers the same session.</summary>
   public bool MatchesPinFilter { get; private set; }
   public bool ReadPin(HashSet<string> pins, IReadOnlySet<SessionId> running)
   {
      var acquiredIdentity = Latest is not null && pins.Remove(Key);
      if(acquiredIdentity) pins.Add(PinKey);
      IsPinned = pins.Contains(PinKey);
      MatchesPinFilter = IsPinned && (Running is not null || Latest is null || !running.Contains(Latest.Id));
      return acquiredIdentity;
   }
   public double Opacity => HasHost ? 1 : 0.6;
   public string Name { get => field; private set => SetProperty(ref field, value); } = "";
   public string Activity { get => field; private set => SetProperty(ref field, value); } = "";
   public SessionTabHeading? Heading { get => field; private set => SetProperty(ref field, value); }
   public bool HasActivity => Activity.Length != 0;

   public void Update(SessionHostOfTheHub? host, RunningSession? running, Session? latest, AgentCli? knownAgent, Project? workingFolder)
   {
      Host = host;
      WorkingFolder = workingFolder;
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
