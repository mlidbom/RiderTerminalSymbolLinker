namespace Cloyd.DotNetAnalysisAndRefactoring.Contract;

/// <summary>
/// A type, as <c>browse_namespace</c> lists it: its <see cref="Name"/>, the bare C# identifier, its members when they were
/// asked for, and the types nested in it.
/// </summary>
sealed record TypeListing(string Kind, string Name, IReadOnlyList<MemberListing>? Members, IReadOnlyList<TypeListing>? NestedTypes);
