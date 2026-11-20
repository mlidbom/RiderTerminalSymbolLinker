using System.Text.Json.Serialization;

namespace Cloyd.LocalApi.Contract;

/// <summary>
/// The body of an answer that says why the Cloyd application didn't do what it was asked, for a human: a request without
/// the token (401), with another <c>Host</c> (403), with a body or query that is no request of the endpoint (400), for
/// what Cloyd doesn't show, such as a session no tab runs (404), and a failure in answering (500).
/// </summary>
/// <param name="Error">Why, for a human.</param>
sealed record ErrorAnswer([property: JsonPropertyName("error")] string Error);
