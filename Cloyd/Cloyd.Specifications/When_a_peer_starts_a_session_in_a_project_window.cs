using System.Net;
using System.Net.Http.Json;
using Cloyd.LocalApi.Contract;
using Cloyd.Projects;
using Cloyd.Sessions;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

public class When_a_peer_starts_a_session_in_a_project_window : SpecificationRunningTheCloydApplication
{
   readonly SandboxProject _project;
   readonly SandboxProject _worktree;

   public When_a_peer_starts_a_session_in_a_project_window()
   {
      _project = Sandbox.Project("project");
      _worktree = Sandbox.Project("other-checkout");
   }

   private protected override IReadOnlyList<SandboxProject> Projects => [_project, _worktree];

   [XF] public void a_Claude_helper_is_visible_in_the_project_and_a_repeated_launch_returns_its_id() => VerifyLaunch("claude", AgentCli.ClaudeCode);
   [XF] public void a_Codex_helper_is_visible_in_the_project_and_a_repeated_launch_returns_its_id() => VerifyLaunch("codex", AgentCli.Codex);

   void VerifyLaunch(string kind, AgentCli cli)
   {
      HandedOver(_project);
      var window = OnTheUIThread(() => Windows.Open.Single());
      const string name = "Investigate compiler warnings";
      DoneOnTheUIThread(() => window.StartANewSession(new NewSession(cli, name)));
      var first = OnTheUIThread(() => window.SelectedTab!);
      var request = new SessionToStartForAPeer(Guid.NewGuid(), new string('A', 64), _project.Project.Folder, _worktree.Project.Folder, kind, name);
      var address = Start(request);
      var helper = OnTheUIThread(() => window.Tabs.Single(it => it != first));
      helper.Host.Directory.Must().Be(_worktree.Project.Folder);
      OnTheUIThread(() => window.SelectedTab).Must().ReferenceEqual(first);
      address.Must().Be($"{kind}:{OnTheUIThread(() => helper.SessionRunningInIt!.Session.Id.Value)}");

      // Discarding the first reply and asking again must yield the original tab, also after a window reopen.
      Close(window);
      HandedOver(_project);
      Start(request).Must().Be(address);
      OnTheUIThread(() => Windows.Open.Single().Tabs.Count).Must().Be(2);
      using var conflict = Posted("sessions/start", JsonContent.Create(request with { Name = "another task" }));
      conflict.StatusCode.Must().Be(HttpStatusCode.Conflict);
   }

   string Start(SessionToStartForAPeer request)
   {
      using var answer = Posted("sessions/start", JsonContent.Create(request));
      answer.EnsureSuccessStatusCode();
      return answer.Content.ReadFromJsonAsync<PeerSessionStarted>().GetAwaiter().GetResult()!.Address;
   }
}
