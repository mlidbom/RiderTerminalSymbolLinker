using Cloyd.Hub;
using Cloyd.Projects;
using Cloyd.Sessions;

namespace Cloyd.History;

abstract partial class WhereTheSessionIs
{
   /// <summary>
   /// It runs in the session host <paramref name="host"/>, which no tab of Cloyd's shows: one whose window has closed, or
   /// that a console shows, as <c>ClaudeTooling.SessionHost.exe attach</c> does. Choosing it shows the host in a tab of
   /// the picker's window, which takes the session over from the console.
   /// </summary>
   public sealed class RunningInAHostNoTabShows(SessionHostOfTheHub host) : WhereTheSessionIs
   {
      public SessionHostOfTheHub Host { get; } = host;

      public override bool IsRunning => true;

      public override string AsToldIn(ProjectWindow pickedIn) => Host.Attached ? "running, shown outside Cloyd" : "running, in no tab";

      public override Task Choose(Session session, ProjectWindow pickedIn)
      {
         pickedIn.ShowInATab(Host, session);
         return Task.CompletedTask;
      }
   }
}
