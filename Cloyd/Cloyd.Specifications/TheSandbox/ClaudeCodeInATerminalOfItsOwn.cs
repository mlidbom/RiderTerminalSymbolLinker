using System.Diagnostics;
using Cloyd.ClaudeCode;

namespace Cloyd.Specifications.TheSandbox;

/// <summary>
/// The stand-in for Claude Code run as Magnus runs Claude Code in a terminal of its own: outside every session host, so
/// that no tab of Cloyd's can show it. With its input and output redirected, it takes each line written to it as a
/// prompt, and ends as its input ends, which <see cref="Dispose"/> ends.
/// </summary>
sealed class ClaudeCodeInATerminalOfItsOwn : IDisposable
{
   static readonly TimeSpan HowLongEndingMayTake = TimeSpan.FromSeconds(10);

   readonly System.Diagnostics.Process _process;

   ClaudeCodeInATerminalOfItsOwn(System.Diagnostics.Process process) => _process = process;

   /// <summary>
   /// A new session named <paramref name="name"/>, in <paramref name="folder"/>, given <paramref name="prompt"/>: the stand-in
   /// of <paramref name="hub"/>, in its <see cref="SandboxHub.Environment"/>, so that it keeps its record and transcript
   /// where the hub reads them.
   /// </summary>
   public static ClaudeCodeInATerminalOfItsOwn Running(SandboxHub hub, string folder, string name, string prompt)
   {
      var start = new ProcessStartInfo(Path.Combine(hub.FolderOfTheStandInForClaudeCode, "claude.exe"), ["--name", name])
      {
         WorkingDirectory = folder,
         RedirectStandardInput = true,
         RedirectStandardOutput = true,
         RedirectStandardError = true,
         CreateNoWindow = true
      };
      foreach(var (variable, value) in hub.Environment) start.Environment[variable] = value;
      var process = System.Diagnostics.Process.Start(start)!;
      process.OutputDataReceived += (_, _) => {};
      process.ErrorDataReceived += (_, _) => {};
      process.BeginOutputReadLine();
      process.BeginErrorReadLine();
      process.StandardInput.WriteLine(prompt);
      process.StandardInput.Flush();
      return new ClaudeCodeInATerminalOfItsOwn(process);
   }

   /// <summary>The process the stand-in runs as, which the hub lists it by.</summary>
   public ClaudeCodeProcessId Process => new(_process.Id);

   /// <summary>Ends the stand-in's input, which ends it, as Claude Code exits.</summary>
   public void Dispose()
   {
      _process.StandardInput.Close();
      if(!_process.WaitForExit(HowLongEndingMayTake)) throw new TimeoutException($"The stand-in for Claude Code, process {_process.Id}, didn't end within {HowLongEndingMayTake.TotalSeconds:0} s of the end of its input.");
      _process.Dispose();
   }
}
