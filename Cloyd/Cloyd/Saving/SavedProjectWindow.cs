using ClaudeTooling.SessionHost.Protocol;
using Cloyd.VirtualDesktops;

namespace Cloyd.Saving;

/// <summary>What a <see cref="Projects.ProjectWindow"/> saves of itself: its tabs, in their order, which is selected, and the virtual desktop it is on.</summary>
/// <param name="Tabs">The window's tabs, in their order.</param>
/// <param name="SelectedTab">The session host of the selected tab: null when no tab was.</param>
/// <param name="Desktop">
/// The virtual desktop the window was on: null when it was on none it knew of, as a window of Avalonia's headless platform
/// that went to none, and in what a Cloyd saved before windows saved their desktops.
/// </param>
sealed record SavedProjectWindow(IReadOnlyList<SavedSessionTab> Tabs, SessionHostId? SelectedTab, VirtualDesktop? Desktop)
{
   /// <summary>What a project whose window was never saved has saved: no tabs, and no desktop.</summary>
   public static SavedProjectWindow NothingSaved { get; } = new([], null, null);
}
