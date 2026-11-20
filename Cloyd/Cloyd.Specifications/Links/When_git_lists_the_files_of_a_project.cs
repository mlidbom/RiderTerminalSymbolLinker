using System.Diagnostics;
using Cloyd.Links.Files;
using Cloyd.Projects;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Links;

/// <summary>
/// The files of a project in a git repository, as <see cref="ProjectFiles.ListedByGitIn"/> reads them: those git tracks,
/// and the new ones it doesn't ignore, so that a file Claude Code just wrote links, and nothing a build wrote does.
/// </summary>
public class When_git_lists_the_files_of_a_project : IDisposable
{
   readonly DirectoryInfo _repository = Directory.CreateTempSubdirectory("cloyd-specification-");
   readonly ProjectFiles _files;

   public When_git_lists_the_files_of_a_project()
   {
      Git("init", "--quiet");
      Write(".gitignore", "bin/\n");
      Write(@"Tracked\One.cs", "");
      Git("add", ".");
      Write("New.cs", "");
      Write(@"bin\Built.dll", "");
      _files = ProjectFiles.ListedByGitIn(new Project(_repository.FullName), CancellationToken.None).GetAwaiter().GetResult();
   }

   [XF] public void a_tracked_file_is_one() => _files.WhosePathEndsWith("One.cs").Must().SequenceEqual([Path.Combine(_repository.FullName, "Tracked", "One.cs")]);

   [XF] public void a_new_file_git_doesnt_ignore_is_one() => _files.HasOneWhosePathEndsWith("New.cs").Must().BeTrue();

   [XF] public void a_file_git_ignores_is_none() => _files.HasOneWhosePathEndsWith("Built.dll").Must().BeFalse();

   void Write(string path, string text)
   {
      var file = new FileInfo(Path.Combine(_repository.FullName, path));
      file.Directory!.Create();
      File.WriteAllText(file.FullName, text);
   }

   void Git(params string[] arguments)
   {
      using var git = Process.Start(new ProcessStartInfo("git", arguments) { WorkingDirectory = _repository.FullName, CreateNoWindow = true })!;
      git.WaitForExit();
      git.ExitCode.Must().Be(0);
   }

   /// <summary>Deletes the repository, whose object files git writes read-only.</summary>
   public void Dispose()
   {
      foreach(var file in _repository.EnumerateFiles("*", SearchOption.AllDirectories)) file.Attributes = FileAttributes.Normal;
      _repository.Delete(recursive: true);
   }
}
