using Cloyd.Hub;
using Cloyd.Hub.RunningSessions;
using Cloyd.Projects;
using Cloyd.Sessions;

namespace Cloyd.History;

/// <summary>One conversation in Cloyd's session list. A newly running agent can appear before its first
/// saved conversation. The same identity retains its recorded history after the agent exits.</summary>
sealed record SessionListing(Session Session, SessionOnDisk? Recorded, RunningSession? Running)
{
   public static SessionListing FromDisk(SessionOnDisk recorded) => new(recorded.Session, recorded, null);

   public static SessionListing WhileRunning(RunningSession agent, SessionOnDisk? recorded)
   {
      var live = agent.Session;
      var session = live.Name is null && recorded?.Session.Name is not null ? recorded.Session : live;
      return new SessionListing(session, recorded, agent);
   }

   public string? Directory => Running?.WorkingFolder?.Folder ?? Recorded?.Directory;
   /// <summary>The branch recorded for the folder in <see cref="Directory"/>. A session now running
   /// in a different checkout no longer describes its recorded branch.</summary>
   public string? Branch
   {
      get
      {
         if(Running?.WorkingFolder is not {} current) return Recorded?.Branch;
         if(Recorded?.Directory is not {} recorded || !Path.IsPathFullyQualified(recorded)) return null;
         return current.Equals(new Project(recorded)) ? Recorded.Branch : null;
      }
   }
   string? Preview => Recorded?.LastPrompt ?? Recorded?.FirstPrompt;
   public string Title => Session.Name ?? Preview ?? Session.Id.Value;
   public string? Prompt => Session.Name is null ? null : Preview;
}
