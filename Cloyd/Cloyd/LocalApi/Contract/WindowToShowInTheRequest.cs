using System.Text.Json.Serialization;

namespace Cloyd.LocalApi.Contract;

/// <summary>
/// The project whose window to show, as the body of <c>POST /windows/show</c> says, such as
/// <c>{"project": "C:\\Dev\\Project"}</c>. Answered with the window shown, a <see cref="WindowInTheAnswer"/>.
/// </summary>
/// <param name="Project">The project's folder, by its full path. Required; it must exist.</param>
sealed record WindowToShowInTheRequest([property: JsonPropertyName("project")] string? Project);
