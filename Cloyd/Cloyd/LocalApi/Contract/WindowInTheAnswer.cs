using System.Text.Json.Serialization;

namespace Cloyd.LocalApi.Contract;

/// <summary>A project window of the Cloyd application, as its local API answers with it.</summary>
/// <param name="Project">The project's folder, by its full path.</param>
/// <param name="Title">The window's title.</param>
sealed record WindowInTheAnswer([property: JsonPropertyName("project")] string Project,
                                       [property: JsonPropertyName("title")] string Title);
