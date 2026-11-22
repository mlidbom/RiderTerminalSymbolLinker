using System.Diagnostics;
using Cloyd.Hub.RunningSessions;
using Cloyd.Sessions;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

/// <summary>Pairs the real Peers MCP servers with a private Cloyd, hub, session hosts and stand-ins.
/// The separate checkout's trial drives the same tools a coordinator receives.</summary>
public class When_Peers_briefs_a_helper_in_Cloyd : SpecificationRunningTheCloydApplication
{
   readonly SandboxProject _project;
   readonly SandboxProject _worktree;

   public static bool HasPeersTrial => Environment.GetEnvironmentVariable("CLOYD_PEERS_LAUNCH_TRIAL") is { Length: > 0 };

   public When_Peers_briefs_a_helper_in_Cloyd() : base(withPeersChannel: true)
   {
      _project = Sandbox.Project("project");
      _worktree = Sandbox.Project("checkout");
   }

   private protected override IReadOnlyList<SandboxProject> Projects => [_project, _worktree];

   [XF(Skip = "Run with the paired Peers build and launch trial.", SkipUnless = nameof(HasPeersTrial))]
   public void a_Claude_coordinator_can_launch_and_contact_both_kinds_of_helper() => VerifyCoordinator("claude");

   [XF(Skip = "Run with the paired Peers build and launch trial.", SkipUnless = nameof(HasPeersTrial))]
   public void a_Codex_coordinator_can_launch_and_contact_both_kinds_of_helper() => VerifyCoordinator("codex");

   void VerifyCoordinator(string sender)
   {
      var bin = CloydsFolder.Folder.CreateSubdirectory("bin");
      foreach(var file in new DirectoryInfo(AppContext.BaseDirectory).EnumerateFiles().Where(it => it.Extension == ".dll" || it.Name.StartsWith("Cloyd.", StringComparison.Ordinal)))
         file.CopyTo(Path.Combine(bin.FullName, file.Name), overwrite: true);
      HandedOver(_project);
      var window = OnTheUIThread(() => Windows.Open.Single());
      DoneOnTheUIThread(() => window.StartANewSession(new NewSession(AgentCli.ClaudeCode, "private-coordinator")));
      WaitUntil(() => window.SelectedTab!.SessionRunningInIt is RunningClaudeCode, "the private Claude coordinator was ready");
      var coordinator = OnTheUIThread(() => (RunningClaudeCode)window.SelectedTab!.SessionRunningInIt!);
      var hub = Sandbox.HubOfTheSandbox;
      var start = new ProcessStartInfo("python") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
      foreach(var (key, value) in hub.Environment) start.Environment[key] = value;
      start.Environment["CLOYD_FOLDER"] = CloydsFolder.Folder.FullName;
      start.Environment["PEERS_STAND_IN_FOR_CODEX"] = Path.Combine(hub.FolderOfTheStandInForCodex, "codex.exe");
      foreach(var argument in new[] { Environment.GetEnvironmentVariable("CLOYD_PEERS_LAUNCH_TRIAL")!, _project.Project.Folder, _worktree.Project.Folder, coordinator.Process.ToString(), sender })
         start.ArgumentList.Add(argument);
      using var process = Process.Start(start)!;
      try
      {
         WaitUntil(() => process.HasExited, "Peers completed the paired trial", timeout: TimeSpan.FromMinutes(3));
         if(process.ExitCode != 0) throw new InvalidOperationException(process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd());
         OnTheUIThread(() => window.Tabs.Count).Must().Be(3);
      }
      finally
      {
         if(!process.HasExited) process.Kill(entireProcessTree: true);
      }
   }
}
