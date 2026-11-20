using Avalonia.Controls;
using CloydSpike.SessionHosts;

namespace CloydSpike.TuiHosts;

/// <summary>
/// One candidate for the TUI's host: a control that draws what a session host's pseudo console draws, and takes the keys,
/// the mouse, pastes and its size to it.
/// </summary>
abstract class TuiHost
{
   /// <summary>What the spike calls it, as <c>dev_docs/cloyd-as-a-dotnet-application.md</c> names the candidate.</summary>
   public abstract string Name { get; }

   /// <summary>Whether it can be drawn on Avalonia's headless platform, for the probe: a native web view can't.</summary>
   public virtual bool DrawsHeadless => true;

   /// <summary>A control showing the session host <paramref name="attachment"/> is attached to, from now on.</summary>
   public abstract Control Showing(AttachmentToASessionHost attachment);

   public override string ToString() => Name;

   public static IReadOnlyList<TuiHost> Candidates { get; } =
      [new IciclecreekTuiHost(), new AgentSpaceTuiHost(), new XtermJsTuiHost()];
}
