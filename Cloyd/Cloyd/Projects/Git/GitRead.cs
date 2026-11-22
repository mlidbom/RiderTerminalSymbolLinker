using System.Diagnostics;
using System.Text;

namespace Cloyd.Projects.Git;

/// <summary>A local, read-only Git command in a checkout. Each argument is passed independently.
/// Disabling optional locks also keeps status from writing the index during dashboard refreshes.</summary>
sealed record GitRead(int ExitCode, string Output, string Error)
{
   public static async Task<GitRead> In(Project folder, CancellationToken cancellation, params string[] arguments)
   {
      cancellation.ThrowIfCancellationRequested();
      var start = new ProcessStartInfo("git", ["--no-optional-locks", "-C", folder.Folder, .. arguments])
      {
         CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
         StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
         Environment = { ["LC_ALL"] = "C", ["GIT_NO_LAZY_FETCH"] = "1" }
      };
      using var git = Process.Start(start)!;
      var output = git.StandardOutput.ReadToEndAsync();
      var error = git.StandardError.ReadToEndAsync();
      try { await git.WaitForExitAsync(cancellation); }
      catch(OperationCanceledException) when(cancellation.IsCancellationRequested)
      {
         if(!git.HasExited) git.Kill(entireProcessTree: true);
         await git.WaitForExitAsync();
         await Task.WhenAll(output, error);
         throw;
      }
      return new GitRead(git.ExitCode, await output, await error);
   }

   public string CheckedOutput() => ExitCode == 0 ? Output : throw new InvalidOperationException(Error.TrimEnd());
}
