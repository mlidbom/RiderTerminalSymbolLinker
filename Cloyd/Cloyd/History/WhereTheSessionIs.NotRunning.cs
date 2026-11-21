using Cloyd.ClaudeCode;
using Cloyd.Projects;

namespace Cloyd.History;

abstract partial class WhereTheSessionIs
{
   /// <summary>
   /// No Claude Code runs it, and no tab ran it last. Choosing it resumes it in a new tab of the picker's window, in the
   /// project's folder, whichever folder it ran in: Claude Code finds its transcript, and goes on with it there.
   /// </summary>
   public sealed class NotRunning : WhereTheSessionIs
   {
      NotRunning() {}

      public static NotRunning Anywhere { get; } = new();

      public override bool IsRunning => false;

      public override string? AsToldIn(ProjectWindow pickedIn) => null;

      public override Task Choose(ClaudeCodeSession session, ProjectWindow pickedIn) => pickedIn.ResumeInANewTab(session);
   }
}
