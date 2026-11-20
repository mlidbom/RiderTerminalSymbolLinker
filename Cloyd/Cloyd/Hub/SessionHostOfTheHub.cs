using ClaudeTooling.Hub.LocalApi.Contract.HostingSessions;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Projects;
using Cloyd.SessionHosts;

namespace Cloyd.Hub;

/// <summary>
/// A session host, as the hub's <c>GET /session-hosts</c> lists it, with the fields Cloyd uses: a process outside every
/// IDE that owns a pseudo console with a session's shell in it, and Claude Code in the shell, so that the session
/// outlives whatever shows it.
/// </summary>
/// <param name="Id">The host's id, which its pipe is named after.</param>
/// <param name="ShellProcessId">The process of the shell in its console: what the hub types <c>claude</c> into.</param>
/// <param name="Directory">The directory the shell started in.</param>
/// <param name="Label">What the one who asked for the host gave to know it by, as <see cref="SessionHostLabel"/> reads it: null for nothing.</param>
/// <param name="Attached">Whether an attach client shows the host now, as an IDE's tab or a Cloyd window does.</param>
sealed record SessionHostOfTheHub(SessionHostId Id, int ShellProcessId, string Directory, string? Label, bool Attached)
{
   /// <summary>The host as the hub's answer <paramref name="host"/> lists it.</summary>
   public static SessionHostOfTheHub InThe(SessionHostInTheAnswer host) =>
      new(new SessionHostId(host.Id), host.ShellProcessId, host.Directory, host.Label, host.Attached);

   /// <summary>The project whose window shows the host: null for a host that no project window shows, as another tool's.</summary>
   public Project? Project => SessionHostLabel.ProjectOf(Label);

   public override string ToString() => $"{Id} in {Directory}{(Attached ? ", attached" : "")}";
}
