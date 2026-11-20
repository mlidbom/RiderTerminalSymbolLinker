using System.Diagnostics;
using Compze.Must;

namespace Cloyd.Specifications.TheSandbox;

/// <summary>A git repository in a folder of a specification's, with files that it has git track, and files it doesn't.</summary>
sealed class GitRepositoryOfASpecification
{
   GitRepositoryOfASpecification(DirectoryInfo folder) => Folder = folder;

   public DirectoryInfo Folder { get; }

   /// <summary>A new repository in <paramref name="folder"/>.</summary>
   public static GitRepositoryOfASpecification In(DirectoryInfo folder)
   {
      var repository = new GitRepositoryOfASpecification(folder);
      repository.Git("init", "--quiet");
      return repository;
   }

   /// <summary>Writes <paramref name="text"/> to the file <paramref name="path"/>, in the repository's folder, and has git track it.</summary>
   public void Track(string path, string text)
   {
      Write(path, text);
      Git("add", path);
   }

   /// <summary>Writes <paramref name="text"/> to the file <paramref name="path"/>, in the repository's folder, as a new file git doesn't track.</summary>
   public void Write(string path, string text)
   {
      var file = new FileInfo(Path.Combine(Folder.FullName, path));
      file.Directory!.Create();
      File.WriteAllText(file.FullName, text);
   }

   /// <summary>
   /// Has the repository a second worktree, in <paramref name="folder"/>, with <paramref name="branch"/> checked out, after
   /// a first commit, empty, which a worktree needs.
   /// </summary>
   public void HasAWorktreeIn(DirectoryInfo folder, string branch)
   {
      Git("-c", "user.name=Specification", "-c", "user.email=specification@localhost", "-c", "commit.gpgsign=false", "commit", "--allow-empty", "--quiet", "-m", "The first commit");
      Git("worktree", "add", "--quiet", "-B", branch, folder.FullName);
   }

   /// <summary>Deletes the repository's folder, whose object files git writes read-only.</summary>
   public void Delete()
   {
      foreach(var file in Folder.EnumerateFiles("*", SearchOption.AllDirectories)) file.Attributes = FileAttributes.Normal;
      Folder.Delete(recursive: true);
   }

   void Git(params string[] arguments) => Succeeds(arguments).Must().BeTrue();

   /// <summary>Whether git, run with <paramref name="arguments"/> in the repository's folder, exits with 0.</summary>
   bool Succeeds(params string[] arguments)
   {
      using var git = Process.Start(new ProcessStartInfo("git", arguments) { WorkingDirectory = Folder.FullName, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true })!;
      git.StandardOutput.ReadToEnd();
      git.StandardError.ReadToEnd();
      git.WaitForExit();
      return git.ExitCode == 0;
   }
}
