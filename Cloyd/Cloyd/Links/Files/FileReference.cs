namespace Cloyd.Links.Files;

/// <summary>
/// A path in a line of a terminal's output that names one or more of the project's files (<see cref="ProjectFiles"/>):
/// what a link to the file covers, the path as written, and the place in the file that the text after the path names.
/// </summary>
/// <param name="Span">What the link covers: the path, and the text after it that names a place in the file.</param>
/// <param name="Path">The path as the line writes it, with either separator, and without the text that names the place.</param>
/// <param name="Place">
/// The place in the file that <c>:63</c>, <c>:63-71</c>, <c>:63:5</c> or <c>#L63-L71</c> after the path names: null when
/// nothing does.
/// </param>
sealed record FileReference(SpanOfALine Span, string Path, PlaceInAFile? Place);
