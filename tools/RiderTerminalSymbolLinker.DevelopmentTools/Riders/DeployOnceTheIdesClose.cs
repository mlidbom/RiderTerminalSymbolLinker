using System.Diagnostics;
using RiderTerminalSymbolLinker.DevelopmentTools.Plugin;
using static Vanara.PInvoke.User32;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   A deploy that waits until Magnus has closed his IDEs, then deploys, and tells him on screen to start them again:
///   for a build that changes what a plugin registers, which a running IDE can't take, while the agent that would run
///   the deploy runs inside one of those IDEs, and ends with it.
/// </summary>
/// <remarks>
///   It waits in a one-shot scheduled task, which closing the IDEs doesn't end, as it does the agent's session, and which
///   runs in Magnus's desktop session, where he sees its window and its message. The task runs a copy of these tools, in
///   <see cref="RunnerFolder"/> beside their build output, so that building them meanwhile isn't refused for files in
///   use. Once done, it removes itself.
/// </remarks>
static class DeployOnceTheIdesClose
{
   const string TaskName = "Deploy the plugins once the IDEs close";

   static readonly TimeSpan HowLongItWaits = TimeSpan.FromHours(3);

   static readonly TimeSpan HowLongTheIdesTakeToLetGo = TimeSpan.FromSeconds(5);

   /// <summary>Where the task's copy of these tools runs from: inside the repository, so that they find it.</summary>
   static string RunnerFolder => Path.Combine(RepositoryRoot.Folder, "tools", "RiderTerminalSymbolLinker.DevelopmentTools", "bin", "deploy-once-the-ides-close");

   /// <summary>
   ///   Says what the deploy would do, as <c>everyday deploy --dry-run</c> does, and sets the deploy waiting for the IDEs
   ///   to close. Returns what it did, a line for each.
   /// </summary>
   public static IReadOnlyList<string> SetUp()
   {
      if(IsWaiting()) return ["A deploy waits for the IDEs to close already: close them, and it deploys."];
      var wouldDo = EverydayIdes.Deploy(new GradleSandbox(), dryRun: true);
      TakeACopyOfTheTools();
      RunTheTask();
      var running = TheIdesThatRun();
      return
      [
         .. wouldDo,
         running.Count == 0
            ? "No IDE runs: the deploy runs at once, and a message says when it's done."
            : $"WAITING: the deploy runs once {NamesOf(running)} {(running.Count == 1 ? "has" : "have")} closed, and a message then says to start them again."
      ];
   }

   static string NamesOf(IEnumerable<EverydayIde> ides) => string.Join(" and ", ides.Select(it => it.ToString()));

   /// <summary>
   ///   The task's own run: waits until no IDE of Magnus's runs, deploys, and tells him how it went in a message,
   ///   whatever happens, then removes the task.
   /// </summary>
   public static void WaitThenDeploy()
   {
      Console.Title = TaskName;
      try
      {
         var clock = Stopwatch.StartNew();
         Console.WriteLine($"Waiting for {NamesOf(TheIdesThatRun())} to close.");
         while(TheIdesThatRun().Count > 0)
         {
            if(clock.Elapsed > HowLongItWaits)
            {
               TellMagnus($"Nothing was deployed: {NamesOf(TheIdesThatRun())} still ran after {HowLongItWaits.TotalHours} hours.", MB_FLAGS.MB_ICONWARNING);
               return;
            }

            Thread.Sleep(TimeSpan.FromSeconds(2));
         }

         Thread.Sleep(HowLongTheIdesTakeToLetGo);
         Console.WriteLine("No IDE runs. Deploying.");
         var did = EverydayIdes.Deploy(new GradleSandbox());
         foreach(var line in did) Console.WriteLine(line);
         TellMagnus($"Deployed. Start your IDEs again.\n\n{string.Join("\n\n", did)}", MB_FLAGS.MB_ICONINFORMATION);
      }
      catch(Exception failure) // It's the task's entry point: Magnus hears of any failure, and the IDEs have what they had.
      {
         Console.WriteLine(failure);
         TellMagnus($"The deploy failed: start your IDEs again, which have the builds they had.\n\n{failure.Message}", MB_FLAGS.MB_ICONERROR);
      }
      finally
      {
         RunSilently("schtasks.exe", ["/Delete", "/TN", TaskName, "/F"]);
      }
   }

   static List<EverydayIde> TheIdesThatRun() => [.. EverydayIde.All.Where(it => it.IsInstalled && it.IsRunning)];

   static bool IsWaiting() => RunSilently("schtasks.exe", ["/Query", "/TN", TaskName]) == 0;

   /// <summary>Copies these tools' build output to <see cref="RunnerFolder"/>, in place of an earlier copy.</summary>
   static void TakeACopyOfTheTools()
   {
      if(Directory.Exists(RunnerFolder)) Directory.Delete(RunnerFolder, recursive: true);
      foreach(var file in Directory.GetFiles(AppContext.BaseDirectory, "*", SearchOption.AllDirectories))
      {
         var copy = Path.Combine(RunnerFolder, Path.GetRelativePath(AppContext.BaseDirectory, file));
         Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
         File.Copy(file, copy);
      }
   }

   /// <summary>Registers the one-shot task that runs <see cref="WaitThenDeploy"/> from the copy, in Magnus's desktop session, and starts it.</summary>
   static void RunTheTask()
   {
      var tools = Path.Combine(RunnerFolder, Path.GetFileName(Environment.ProcessPath!));
      var script = $"""
                    $action = New-ScheduledTaskAction -Execute '{tools.Replace("'", "''")}' -Argument 'everyday deploy --after-the-ides-close' -WorkingDirectory '{RepositoryRoot.Folder.Replace("'", "''")}'
                    $principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive
                    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Hours 4)
                    Register-ScheduledTask -TaskName '{TaskName}' -Action $action -Principal $principal -Settings $settings -Force | Out-Null
                    Start-ScheduledTask -TaskName '{TaskName}'
                    """;
      var exitCode = RunSilently("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", script]);
      if(exitCode != 0) throw new InvalidOperationException($"Registering the scheduled task \"{TaskName}\" failed with exit code {exitCode}.");
   }

   static void TellMagnus(string text, MB_FLAGS icon) =>
      MessageBox(default, text, TaskName, MB_FLAGS.MB_OK | icon | MB_FLAGS.MB_TOPMOST | MB_FLAGS.MB_SETFOREGROUND);

   /// <summary>Runs <paramref name="program"/> with no window, its output discarded, and returns its exit code.</summary>
   static int RunSilently(string program, IReadOnlyList<string> arguments)
   {
      var start = new ProcessStartInfo(program) { CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
      foreach(var argument in arguments) start.ArgumentList.Add(argument);
      using var process = Process.Start(start)!;
      var errors = process.StandardError.ReadToEndAsync(); // Both at once: either filling up would block the program.
      process.StandardOutput.ReadToEnd();
      errors.Wait();
      process.WaitForExit();
      return process.ExitCode;
   }
}
