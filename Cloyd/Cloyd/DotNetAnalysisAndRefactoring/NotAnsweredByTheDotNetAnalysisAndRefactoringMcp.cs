namespace Cloyd.DotNetAnalysisAndRefactoring;

/// <summary>
/// The <see cref="DotNetAnalysisAndRefactoringMcp"/> couldn't answer now, and why, for a human: nothing answers on its
/// port, as while Rider doesn't run; it answered an error, as while Rider still loads a solution; or it answered in text,
/// being older than the JSON form.
/// </summary>
sealed class NotAnsweredByTheDotNetAnalysisAndRefactoringMcp(string why) : Exception(why);
