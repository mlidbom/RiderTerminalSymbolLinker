namespace RiderTerminalSymbolLinker.Specifications.ClaudeSessions;

/// <summary>
///   A restart of the sandbox Rider that the sandbox's hub made, as its <c>GET /rider-restart</c> tells of it once done:
///   the kind of its outcome, <c>back</c> once Rider is, and the outcome's headline; the steps it took, as
///   <c>ClaudeTooling.exe rider restart</c> prints them; and the hub's log of its Rider restarts, <c>rider-restarts.log</c>.
/// </summary>
public sealed record RiderRestartByTheHub(string Outcome, string Headline, IReadOnlyList<string> Steps, string Log);
