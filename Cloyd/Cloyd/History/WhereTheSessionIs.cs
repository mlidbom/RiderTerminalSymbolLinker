using Cloyd.Projects;
using Cloyd.Sessions;

namespace Cloyd.History;

/// <summary>
/// Where a session of the <see cref="SessionHistory"/> is now, which decides what choosing it in the
/// <see cref="SessionHistoryPicker"/> does: a tab it runs in is brought forward, and a session that runs nowhere
/// is resumed in a tab.
/// </summary>
/// <remarks>
/// A session that runs is never resumed again: two agent CLIs writing to one session would mix their conversations.
/// </remarks>
abstract partial class WhereTheSessionIs
{
   /// <summary>Whether the session can be chosen in the picker.</summary>
   public virtual bool CanBeChosen => true;

   /// <summary>Whether its agent CLI runs the session, which the picker marks.</summary>
   public abstract bool IsRunning { get; }

   /// <summary>What the picker of <paramref name="pickedIn"/> tells of where the session is: null for nothing, as for one that runs nowhere.</summary>
   public abstract string? AsToldIn(ProjectWindow pickedIn);

   /// <summary>Does what choosing <paramref name="session"/> in the picker of <paramref name="pickedIn"/> does.</summary>
   public abstract Task Choose(Session session, ProjectWindow pickedIn);

   /// <summary>Where <paramref name="window"/> is told to be from the picker of <paramref name="pickedIn"/>: nothing when it's the same window.</summary>
   static string OfAnotherProject(ProjectWindow window, ProjectWindow pickedIn) => window == pickedIn ? "" : $" of {window.Project.Name}";
}
