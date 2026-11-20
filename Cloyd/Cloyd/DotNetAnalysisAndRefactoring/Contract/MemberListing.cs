namespace Cloyd.DotNetAnalysisAndRefactoring.Contract;

/// <summary>
/// A member a type declares, as <c>browse_namespace</c> lists it. Its <see cref="Kind"/> is C#'s keyword where there is
/// one: <c>method</c>, <c>property</c>, <c>field</c> (constants and enum members too), <c>event</c>, <c>constructor</c>,
/// <c>indexer</c>, <c>operator</c>. A C# 14 extension block is a member of the kind <c>extension</c>, with no
/// <see cref="Name"/>, and members of its own.
/// </summary>
sealed record MemberListing(string Kind, string? Name, IReadOnlyList<MemberListing>? Members);
