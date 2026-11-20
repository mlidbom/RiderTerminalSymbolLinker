namespace Cloyd.Links.Symbols;

/// <summary>
/// A C# type or member that a line of a terminal's output names (<see cref="SymbolNames"/>): what a link to its
/// declaration covers, and its name.
/// </summary>
/// <param name="Span">What the link covers: the name, or a type's and its member's, as in <c>ApplicationWindow.ForceToFront</c>.</param>
/// <param name="Name">The name to find the declarations of: a type's or a member's, or <see cref="MemberOfAType.AsCodeWritesIt"/>.</param>
sealed record SymbolReference(SpanOfALine Span, string Name);
