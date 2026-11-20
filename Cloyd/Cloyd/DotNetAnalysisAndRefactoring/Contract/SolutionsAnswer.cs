namespace Cloyd.DotNetAnalysisAndRefactoring.Contract;

/// <summary><c>list_solutions</c>' answer: every solution that Rider has loaded and the MCP serves.</summary>
sealed record SolutionsAnswer(IReadOnlyList<LoadedSolution> Solutions);
