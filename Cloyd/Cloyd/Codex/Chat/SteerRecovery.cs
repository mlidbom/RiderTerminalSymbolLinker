namespace Cloyd.Codex.Chat;

/// <summary>Where Chat still holds the original parts while the delivery of an individual steer is unresolved.</summary>
enum SteerRecovery { AwaitingHistory, AvailableToCopy, CopyReturnedToEditor }
