using System.Diagnostics;
using System.Globalization;
using Compze.Threading;
using Compze.Threading.Interprocess;
using RiderTerminalSymbolLinker.DevelopmentTools.Cloyd;
using RiderTerminalSymbolLinker.DevelopmentTools.Plugin;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   A deploy that has the Claude tooling's hub restart Magnus's IDEs that run, and puts the build in each while it's
///   down: for a build that changes what a plugin registers, which a running IDE can't take. Magnus, 2026-11-20: "I
///   greenlight, you install and restart. My IDEs come back just as they were on the same desktops."
/// </summary>
/// <remarks>
///   The hub restarts the IDEs (<c>POST /ide-restart</c>), as Magnus asks himself, so with no prompt of his, and tells the
///   agents while they're down. Each IDE's Claude Sessions restarts it running <see cref="IntoTheIdesThatClosed"/> once
///   the IDE has exited, before it starts it again (its <c>RestartRunningACommand</c>), as the IDE's restarter: it takes the builds
///   <see cref="Stage"/> put aside, as the working tree may build on meanwhile, and the copy of these tools it runs
///   from. The sessions run on in their session hosts, the tabs attach to them again, and each window goes back to its
///   virtual desktop.<br/>
///   Several IDEs restart at once, each restarter running the deploy as its IDE exits, so the deploys take turns, and
///   each puts the builds into every IDE that doesn't run: into its own, and into one the hub doesn't restart as it
///   doesn't run.
/// </remarks>
static class DeployRestartingTheIdes
{
   /// <summary>The copy of these tools the restarters run.</summary>
   static readonly CopyOfTheTools Runner = new("deploy-restarting-the-ides");

   static string DevelopmentFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RiderTerminalSymbolLinkerDevelopment");

   /// <summary>Where <see cref="Stage"/> puts the builds aside for the restarters.</summary>
   static string StagedBuildsFolder => Path.Combine(DevelopmentFolder, "staged-plugin-builds");

   /// <summary>What each restarter's deploy did, line by line, as no window shows it.</summary>
   static string Log => Path.Combine(DevelopmentFolder, "deploys-restarting-the-ides.log");

   /// <summary>How long the hub may take to have every IDE back: it gives up on an IDE after 15 minutes.</summary>
   static readonly TimeSpan HowLongTheRestartMayTake = TimeSpan.FromMinutes(20);

   /// <summary>
   ///   Puts this working tree's Cloyd in, which Claude Sessions runs, builds the plugins, puts the builds into each IDE
   ///   that doesn't run, and has the hub restart those that run and don't have them, putting them in as they're down. Writes each step, the hub's progress and the outcome to
   ///   <paramref name="output"/>.
   /// </summary>
   public static void Run(TextWriter output)
   {
      output.WriteLine(InstalledCloyd.Everyday.Deploy(CloydBuild.Published(), dryRun: false));
      var builds = GradleSandbox.Filled();
      foreach(var line in EverydayIdes.Deploy(builds, dryRun: true)) output.WriteLine(line);
      var staged = Stage(builds);
      foreach(var line in EverydayIdes.Deploy(staged, onlyInto: it => !it.IsRunning)) output.WriteLine(line);
      var toRestart = EverydayIdes.IdesWithoutTheBuildsIn(staged).Where(it => it.IsRunning).ToList();
      if(toRestart.Count == 0)
      {
         output.WriteLine("No IDE that runs lacks the builds: nothing to restart.");
         return;
      }

      var logWrittenBefore = File.Exists(Log) ? new FileInfo(Log).Length : 0;
      var hub = HubsLocalApi.OfThePublishedHub;
      hub.AskToRestartTheIdes("Claude Sessions or the symbol linker has a new build, which goes in while the IDE is down",
                              "everyday deploy",
                              [.. toRestart.Select(it => it.NameAtTheHub)],
                              [Runner.Executable, "everyday", "deploy", "--into-the-ides-that-closed"]);
      output.WriteLine($"The hub restarts {string.Join(" and ", toRestart)}, and the build goes into each while it's down.");
      FollowTheRestart(hub, output);
      output.WriteLine("What the restarters' deploys did:");
      output.Write(LogWrittenSince(logWrittenBefore));
   }

   /// <summary>
   ///   The restarter's step: puts the staged builds into every IDE that doesn't run, taking turns with the other
   ///   restarters', and logs what it did. An IDE's restarter starts the IDE again once it has ended, whatever came of it.
   /// </summary>
   public static void IntoTheIdesThatClosed()
   {
      try
      {
         using var mutex = IMutex.Local("RiderTerminalSymbolLinker.DeployIntoTheIdesThatClosed", LockTimeout.Minutes(5));
         mutex.Locked(() =>
         {
            foreach(var line in EverydayIdes.Deploy(new StagedPluginBuilds(StagedBuildsFolder), onlyInto: it => !it.IsRunning)) WriteToTheLog(line);
         });
      }
      catch(Exception failure) // The restarter's step: nothing else would tell of the failure, and the IDE starts with the build it had.
      {
         WriteToTheLog($"FAILED to deploy into the IDEs that closed: {failure}");
      }
   }

   /// <summary>Puts the builds in <paramref name="builds"/> aside, and the copy of these tools that the restarters run.</summary>
   static StagedPluginBuilds Stage(IPluginBuilds builds)
   {
      Runner.Take();
      return StagedPluginBuilds.StageFrom(builds, StagedBuildsFolder);
   }

   /// <summary>Writes each step of the hub's restart as it's taken, and its outcome once it's done.</summary>
   static void FollowTheRestart(HubsLocalApi hub, TextWriter output)
   {
      var clock = Stopwatch.StartNew();
      var stepsWritten = 0;
      while(true)
      {
         var restart = hub.TheIdeRestart() ?? throw new InvalidOperationException("The hub tells of no restart of the IDEs.");
         var steps = restart["progress"]!.AsArray();
         for(; stepsWritten < steps.Count; stepsWritten++) output.WriteLine($"  {steps[stepsWritten]!["text"]!.GetValue<string>()}");
         if(restart["stage"]!.GetValue<string>() == "done")
         {
            var outcome = restart["outcome"]!;
            output.WriteLine($"{outcome["headline"]!.GetValue<string>()}: {outcome["description"]!.GetValue<string>()}");
            return;
         }
         if(clock.Elapsed > HowLongTheRestartMayTake) throw new TimeoutException($"The hub's restart of the IDEs wasn't done after {HowLongTheRestartMayTake}.");
         Thread.Sleep(TimeSpan.FromSeconds(1));
      }
   }

   static void WriteToTheLog(string line)
   {
      Directory.CreateDirectory(DevelopmentFolder);
      File.AppendAllLines(Log, [$"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} {line}"]);
   }

   /// <summary>What the log holds past its first <paramref name="bytes"/>: what was written to it since it was that long.</summary>
   static string LogWrittenSince(long bytes)
   {
      if(!File.Exists(Log)) return "";
      using var log = File.OpenRead(Log);
      log.Seek(bytes, SeekOrigin.Begin);
      return new StreamReader(log).ReadToEnd();
   }
}
