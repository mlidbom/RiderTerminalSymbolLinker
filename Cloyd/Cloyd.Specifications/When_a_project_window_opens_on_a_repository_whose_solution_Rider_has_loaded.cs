using Cloyd.DotNetAnalysisAndRefactoring.Contract;
using Cloyd.Links;
using Cloyd.Links.Files;
using Cloyd.Links.Symbols;
using Cloyd.Projects;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

/// <summary>
/// The files and C# symbols that the output in a project window's terminals links to: the files of the project's git
/// repository, and the types and members of the solutions Rider has loaded in the project's folder, which the window
/// reads as it opens (<see cref="ProjectWindow.FilesAndSymbols"/>).
/// </summary>
public class When_a_project_window_opens_on_a_repository_whose_solution_Rider_has_loaded : SpecificationOpeningWindows
{
   readonly ProjectWindow _window;

   public When_a_project_window_opens_on_a_repository_whose_solution_Rider_has_loaded()
   {
      var project = Sandbox.Project("the-project");
      var repository = GitRepositoryOfASpecification.In(new DirectoryInfo(project.Project.Folder));
      repository.Track(@"Code\AType.cs", "");
      Sandbox.DotNetAnalysisAndRefactoringMcp.Load(Path.Combine(project.Project.Folder, "ASolution.slnx"), new Dictionary<string, IReadOnlyList<TypeListing>>
      {
         ["Sandbox.Code"] = [new TypeListing("class", "AType", [new MemberListing("method", "AMember", null)], null)]
      });
      Sandbox.DotNetAnalysisAndRefactoringMcp.Load(Path.Combine(Sandbox.ProjectsFolder, "Elsewhere", "Elsewhere.slnx"), new Dictionary<string, IReadOnlyList<TypeListing>>
      {
         ["Elsewhere"] = [new TypeListing("class", "TypeElsewhere", [], null)]
      });
      _window = Open(project.Window);
      WaitUntil(() => Links.SymbolReferencesIn("AType").Count > 0 && Links.FileReferencesIn("AType.cs").Count > 0, "the window read the project's files and symbols");
   }

   FilesAndSymbolsOfAProject Links => _window.FilesAndSymbols;

   [XF] public void a_types_member_in_the_solution_links() =>
      Links.SymbolReferencesIn("call AType.AMember() now").Must().SequenceEqual([new SymbolReference(new SpanOfALine(5, 18), "AType.AMember")]);

   [XF] public void a_file_git_tracks_links() =>
      Links.FileReferencesIn("see Code/AType.cs:12").Must().SequenceEqual([new FileReference(new SpanOfALine(4, 20), "Code/AType.cs", new PlaceInAFile(12, null, null))]);

   [XF] public void a_type_of_a_solution_outside_the_projects_folder_doesnt_link() => Links.SymbolReferencesIn("TypeElsewhere").Must().BeEmpty();

   [XF] public void it_tells_of_no_failure() => OnTheUIThread(() => _window.Failure).Must().BeNull();
}
