namespace Cloyd.LocalApi;

/// <summary>Host continuity cannot identify the original CLI session after an interrupted start.
/// The same terminal can already be showing a replacement session.</summary>
sealed class UnconfirmedPeerSessionIdentity(string host) : Exception(
   $"The CLI start was requested in host {host}, but its original session ID was never recorded. No brief was sent. Inspect the tab and history. Send the brief manually only after confirming the original ID.");
