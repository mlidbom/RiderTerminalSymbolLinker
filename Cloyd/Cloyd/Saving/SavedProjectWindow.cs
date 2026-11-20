using ClaudeTooling.SessionHost.Protocol;

namespace Cloyd.Saving;

/// <summary>What a <see cref="Projects.ProjectWindow"/> saves of itself: its tabs, in their order, and which is selected.</summary>
/// <param name="Tabs">The window's tabs, in their order.</param>
/// <param name="SelectedTab">The session host of the selected tab: null when no tab was.</param>
sealed record SavedProjectWindow(IReadOnlyList<SavedSessionTab> Tabs, SessionHostId? SelectedTab)
{
   /// <summary>What a project whose window was never saved has saved: no tabs.</summary>
   public static SavedProjectWindow NothingSaved { get; } = new([], null);
}
