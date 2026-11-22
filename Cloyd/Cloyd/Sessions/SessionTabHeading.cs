using Cloyd.Hub.RunningSessions;

namespace Cloyd.Sessions;

/// <summary>The name and agent mark on a session tab in either window.</summary>
sealed record SessionTabHeading(string Title, AgentCli? Agent, RunningSession? Running);
