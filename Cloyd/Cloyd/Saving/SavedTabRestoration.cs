using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Sessions;

namespace Cloyd.Saving;

/// <summary>The part of a <see cref="SavedSessionTab"/> that determines which session a project window
/// restores and where. Changes to a draft need saving without invalidating Projects Dashboard's tab membership.</summary>
readonly record struct SavedTabRestoration(SessionHostId Host, Session? Session, string Directory, string? SessionDirectory)
{
   public static SavedTabRestoration Of(SavedSessionTab tab) => new(tab.SessionHost, tab.Session, tab.Directory, tab.SessionDirectory);
}
