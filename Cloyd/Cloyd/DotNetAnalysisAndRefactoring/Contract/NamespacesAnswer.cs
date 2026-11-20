namespace Cloyd.DotNetAnalysisAndRefactoring.Contract;

/// <summary><c>browse_namespace</c>'s answer: a <see cref="NamespaceListing"/> for each namespace asked for, in the order asked.</summary>
sealed record NamespacesAnswer(IReadOnlyList<NamespaceListing> Namespaces);
