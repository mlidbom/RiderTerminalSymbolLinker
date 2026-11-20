using Cloyd.ClaudeCode;
using Cloyd.Projects;
using Cloyd.Sessions;

namespace Cloyd.History;

abstract partial class WhereTheSessionIs
{
   /// <summary>It runs in <paramref name="tab"/> of <paramref name="window"/>, of any project. Choosing it brings the tab forward.</summary>
   public sealed class RunningInATab(ProjectWindow window, SessionTab tab) : WhereTheSessionIs
   {
      /// <summary>The window of the <see cref="Tab"/>.</summary>
      public ProjectWindow Window { get; } = window;

      public SessionTab Tab { get; } = tab;

      public override bool IsRunning => true;

      public override string AsToldIn(ProjectWindow pickedIn) => $"running in a tab{OfAnotherProject(Window, pickedIn)}";

      public override Task Choose(ClaudeSession session, ProjectWindow pickedIn)
      {
         Window.ShowTheTab(Tab);
         return Task.CompletedTask;
      }
   }
}
