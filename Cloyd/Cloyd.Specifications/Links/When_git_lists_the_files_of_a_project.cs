using Cloyd.Links.Files;
using Cloyd.Projects;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Links;

/// <summary>
/// The files of a project in a git repository, as <see cref="ProjectFiles.ListedByGitIn"/> reads them: those git tracks,
/// and the new ones it doesn't ignore, so that a file Claude Code just wrote links, and nothing a build wrote does.
/// </summary>
public class When_git_lists_the_files_of_a_project : IDisposable
{
   readonly GitRepositoryOfASpecification _repository = GitRepositoryOfASpecification.In(Directory.CreateTempSubdirectory("cloyd-specification-"));
   readonly ProjectFiles _files;

   public When_git_lists_the_files_of_a_project()
   {
      _repository.Track(".gitignore", "bin/\n");
      _repository.Track(@"Tracked\One.cs", "");
      _repository.Write("New.cs", "");
      _repository.Write(@"bin\Built.dll", "");
      _files = ProjectFiles.ListedByGitIn(new Project(_repository.Folder.FullName), CancellationToken.None).GetAwaiter().GetResult();
   }

   [XF] public void a_tracked_file_is_one() => _files.WhosePathEndsWith("One.cs").Must().SequenceEqual([Path.Combine(_repository.Folder.FullName, "Tracked", "One.cs")]);

   [XF] public void a_new_file_git_doesnt_ignore_is_one() => _files.HasOneWhosePathEndsWith("New.cs").Must().BeTrue();

   [XF] public void a_file_git_ignores_is_none() => _files.HasOneWhosePathEndsWith("Built.dll").Must().BeFalse();

   public void Dispose() => _repository.Delete();
}
