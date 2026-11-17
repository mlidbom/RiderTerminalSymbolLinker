using System.Diagnostics;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Processes;

/// <summary>A command line program the tools run to completion, such as Gradle.</summary>
static class ExternalCommand
{
   /// <summary>
   ///   Runs <paramref name="program"/> and waits for it, echoing its output. A nonzero exit code is a failure of the
   ///   tool that ran it, so it throws.
   /// </summary>
   public static void Run(string program, IReadOnlyList<string> arguments, string workingFolder, IReadOnlyDictionary<string, string>? environment = null)
   {
      var startInformation = new ProcessStartInfo(program) { WorkingDirectory = workingFolder, UseShellExecute = false };
      foreach(var argument in arguments) startInformation.ArgumentList.Add(argument);
      foreach(var (name, value) in environment ?? new Dictionary<string, string>()) startInformation.Environment[name] = value;

      using var process = Process.Start(startInformation)
                       ?? throw new InvalidOperationException($"Could not start {program}.");
      process.WaitForExit();
      if(process.ExitCode != 0)
         throw new InvalidOperationException($"{program} {string.Join(' ', arguments)} failed with exit code {process.ExitCode}.");
   }
}
