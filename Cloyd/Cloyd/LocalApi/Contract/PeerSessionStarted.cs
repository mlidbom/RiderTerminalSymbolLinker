using System.Text.Json.Serialization;

namespace Cloyd.LocalApi.Contract;

/// <summary>The actual session observed in the requested tab's host.</summary>
sealed record PeerSessionStarted([property: JsonPropertyName("address")] string Address);
