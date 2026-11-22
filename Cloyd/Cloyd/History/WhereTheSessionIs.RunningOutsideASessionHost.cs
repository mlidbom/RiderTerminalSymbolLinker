using Cloyd.Projects;
using Cloyd.Sessions;

namespace Cloyd.History;

abstract partial class WhereTheSessionIs
{
   /// <summary>
   /// It runs in no session host, as in a terminal of its own: no tab can show it, so it can't be chosen.
   /// </summary>
   public sealed class RunningOutsideASessionHost : WhereTheSessionIs
   {
      public override bool CanBeChosen => false;

      public override bool IsRunning => true;

      public override string AsToldIn(ProjectWindow? pickedIn) => "running outside Cloyd";

      public override Task Choose(Session session, ISessionHistoryTarget pickedIn) =>
         throw new InvalidOperationException($"A session that runs in no session host can't be chosen: {session.Id} runs outside Cloyd.");
   }
}
