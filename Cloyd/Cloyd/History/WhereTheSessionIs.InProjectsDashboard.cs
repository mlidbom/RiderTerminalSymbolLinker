using Cloyd.Hub;
using Cloyd.Projects;
using Cloyd.Sessions;

namespace Cloyd.History;

abstract partial class WhereTheSessionIs
{
   /// <summary>A host in Projects Dashboard with no project tab. If its agent has exited, the picker
   /// resumes the session in the existing shell.</summary>
   public sealed class InProjectsDashboard(SessionHostOfTheHub host, bool running) : WhereTheSessionIs
   {
      public override bool IsRunning => running;
      public override string AsToldIn(ProjectWindow? pickedIn) => running ? "running in Projects Dashboard" : "open in Projects Dashboard";
      public override Task Choose(Session session, ISessionHistoryTarget pickedIn) => pickedIn.ChooseDashboardHost(host, session, resume: !running);
   }
}
