namespace Cloyd.DotNetAnalysisAndRefactoring.Contract;

/// <summary>A tool's answer, as text: JSON, when the tool was asked for its JSON form and is new enough to have one.</summary>
sealed record ToolContent(string Text);
