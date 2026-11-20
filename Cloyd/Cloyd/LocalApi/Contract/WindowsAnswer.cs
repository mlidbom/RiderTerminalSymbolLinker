using System.Text.Json.Serialization;

namespace Cloyd.LocalApi.Contract;

/// <summary>
/// The project windows the Cloyd application shows: the answer to <c>GET /windows</c>. An object around the list, so
/// that fields can be added beside it.
/// </summary>
/// <param name="Windows">Each window, in the order they opened.</param>
sealed record WindowsAnswer([property: JsonPropertyName("windows")] IReadOnlyList<WindowInTheAnswer> Windows);
