using Cloyd.ClaudeCode;
using Cloyd.Projects;
using Cloyd.Sessions;

namespace Cloyd.History;

abstract partial class WhereTheSessionIs
{
   /// <summary>
   /// It ran last in <paramref name="tab"/> of <paramref name="window"/>, whose Claude Code has exited, leaving the shell at
   /// its prompt. Choosing it resumes it in that tab.
   /// </summary>
   public sealed class ExitedInATab(ProjectWindow window, SessionTab tab) : WhereTheSessionIs
   {
      /// <summary>The window of the <see cref="Tab"/>.</summary>
      public ProjectWindow Window { get; } = window;

      public SessionTab Tab { get; } = tab;

      public override bool IsRunning => false;

      public override string AsToldIn(ProjectWindow pickedIn) => $"open in a tab{OfAnotherProject(Window, pickedIn)}";

      public override Task Choose(ClaudeSession session, ProjectWindow pickedIn) => Window.ResumeIn(Tab, session);
   }
}
