namespace Cloyd.LocalApi;

/// <summary>A caller reused another launch's request ID with different arguments.</summary>
sealed class PeerSessionRequestConflict() : Exception("This request ID already belongs to a different launch. Retry with the original arguments.");
