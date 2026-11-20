namespace Cloyd.DotNetAnalysisAndRefactoring.Contract;

/// <summary>
/// What a tool answered: one <see cref="ToolContent"/>, whose text is the answer, in the JSON form that the argument
/// <c>"format": "json"</c> asks for.
/// </summary>
sealed record ToolResult(IReadOnlyList<ToolContent> Content);
