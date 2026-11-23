using System.Diagnostics;
using System.Text;

namespace Cloyd.Projects.Git;

/// <summary>A local Git command in a checkout. Each argument is passed independently.
/// Disabling optional locks also keeps status from writing the index during dashboard refreshes.</summary>
sealed record GitCommand(int ExitCode, string Output, string Error)
{
   public static Task<GitCommand> In(Project folder, CancellationToken cancellation, params string[] arguments) =>
      Run(new ProcessStartInfo("git", ["--no-optional-locks", "-C", folder.Folder, .. arguments]), cancellation);

   public static async Task<GitCommand> Run(ProcessStartInfo start, CancellationToken cancellation, Action<Process>? started = null)
   {
      cancellation.ThrowIfCancellationRequested();
      start.CreateNoWindow = true;
      start.RedirectStandardOutput = start.RedirectStandardError = true;
      start.StandardOutputEncoding = start.StandardErrorEncoding = Encoding.UTF8;
      start.Environment["LC_ALL"] = "C";
      start.Environment["GIT_NO_LAZY_FETCH"] = "1";
      start.Environment["GIT_TERMINAL_PROMPT"] = "0";
      start.Environment["GCM_INTERACTIVE"] = "Never";
      using var git = Process.Start(start)!;
      var output = git.StandardOutput.ReadToEndAsync();
      var error = git.StandardError.ReadToEndAsync();
      try
      {
         try { started?.Invoke(git); }
         finally { await git.WaitForExitAsync(cancellation); }
      }
      catch(OperationCanceledException) when(cancellation.IsCancellationRequested)
      {
         if(!git.HasExited) git.Kill(entireProcessTree: true);
         await git.WaitForExitAsync();
         await Task.WhenAll(output, error);
         throw;
      }
      return new GitCommand(git.ExitCode, await output, await error);
   }

   public string CheckedOutput() => ExitCode == 0 ? Output : throw new InvalidOperationException(Error.TrimEnd());
}
