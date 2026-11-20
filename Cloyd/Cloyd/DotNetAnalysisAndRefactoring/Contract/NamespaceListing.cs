namespace Cloyd.DotNetAnalysisAndRefactoring.Contract;

/// <summary>
/// A namespace, as <c>browse_namespace</c> lists it: the qualified names of the namespaces in it, and its types. Both are
/// empty for a namespace not found, which the MCP gives an error that Cloyd needn't read.
/// </summary>
sealed record NamespaceListing(IReadOnlyList<string> ChildNamespaces, IReadOnlyList<TypeListing> Types);
