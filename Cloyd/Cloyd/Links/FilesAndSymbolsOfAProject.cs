using Cloyd.DotNetAnalysisAndRefactoring;
using Cloyd.Links.Files;
using Cloyd.Links.Symbols;
using Cloyd.Projects;

namespace Cloyd.Links;

/// <summary>
/// The files and the C# symbols of a project, which the output in its terminals links to: its <see cref="ProjectFiles"/>,
/// as git lists them, and the <see cref="SymbolNames"/> of the solutions Rider has loaded in its folder, as the
/// <see cref="DotNetAnalysisAndRefactoringMcp"/> lists them. None of either until they are first read.
/// </summary>
/// <remarks>
/// Read again and again, by whoever keeps them current, as the project's window does, and read from by its terminals:
/// each is swapped whole, so that a reader sees one reading or the next.
/// </remarks>
sealed class FilesAndSymbolsOfAProject(Project project, DotNetAnalysisAndRefactoringMcp dotNetAnalysisAndRefactoringMcp)
{
   readonly Project _project = project;
   readonly DotNetAnalysisAndRefactoringMcp _dotNetAnalysisAndRefactoringMcp = dotNetAnalysisAndRefactoringMcp;
   volatile ProjectFiles _files = new([]);
   volatile SymbolNames _symbols = new([], []);

   public IReadOnlyList<FileReference> FileReferencesIn(string line) => FileReferences.In(line, _files);

   public IReadOnlyList<SymbolReference> SymbolReferencesIn(string line) => SymbolReferences.In(line, _symbols);

   /// <summary>Reads the project's files again, as git lists them now.</summary>
   public async Task ReadTheFiles(CancellationToken cancellation) => _files = await ProjectFiles.ListedByGitIn(_project, cancellation);

   /// <summary>
   /// Gathers the names of the solutions' types and members again: whether the MCP answered. One that doesn't, as while
   /// Rider doesn't run or still loads a solution, leaves the names gathered before.
   /// </summary>
   public async Task<bool> GatherTheSymbols(CancellationToken cancellation)
   {
      try
      {
         _symbols = await _dotNetAnalysisAndRefactoringMcp.SymbolNamesOfTheSolutionsIn(_project, cancellation);
         return true;
      }
      catch(NotAnsweredByTheDotNetAnalysisAndRefactoringMcp)
      {
         return false;
      }
   }
}
