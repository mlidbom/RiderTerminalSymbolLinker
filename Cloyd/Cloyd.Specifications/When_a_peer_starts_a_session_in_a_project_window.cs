using System.Net;
using System.Net.Http.Json;
using Cloyd.LocalApi;
using Cloyd.LocalApi.Contract;
using Cloyd.SessionHosts;
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
      Start(request with { Project = request.Project.ToUpperInvariant(), Worktree = request.Worktree.ToLowerInvariant() }).Must().Be(address);
      OnTheUIThread(() => Windows.Open.Single().Tabs.Count).Must().Be(2);
      using var conflict = Posted("sessions/start", JsonContent.Create(request with { Name = "another task" }));
      conflict.StatusCode.Must().Be(HttpStatusCode.Conflict);
   }

   [XF] public void a_host_created_before_an_interrupted_launch_is_adopted_and_briefing_can_continue()
   {
      HandedOver(_project);
      var request = new SessionToStartForAPeer(Guid.NewGuid(), new string('C', 64), _project.Project.Folder, _worktree.Project.Folder, "claude", "Recovered helper");
      var receipt = new PeerSessionLaunchReceipt(CloydsFolder, request);
      receipt.RecordHostRequest();
      var original = _worktree.StartASessionHost(SessionHostLabel.ForAPeer(_project.Project, request.RequestId));
      var address = Start(request);
      var tab = OnTheUIThread(() => Windows.Open.Single().Tabs.Single());
      tab.Host.Id.Must().Be(original.Id);
      address.Must().Be($"claude:{OnTheUIThread(() => tab.SessionRunningInIt!.Session.Id.Value)}");

      // The native ID was persisted, but its HTTP reply was lost. Reopening still returns that ID.
      var window = OnTheUIThread(() => Windows.Open.Single());
      Close(window);
      Start(request).Must().Be(address);
      OnTheUIThread(() => Windows.Open.Single().Tabs.Single().Host.Id).Must().Be(original.Id);
   }

   [XF] public void a_replacement_Codex_session_cannot_inherit_an_unidentified_launch()
   {
      var request = new SessionToStartForAPeer(Guid.NewGuid(), new string('E', 64), _project.Project.Folder, _worktree.Project.Folder, "codex", "Original assignment");
      var receipt = new PeerSessionLaunchReceipt(CloydsFolder, request);
      receipt.RecordHostRequest();
      var host = _worktree.StartASessionHost(SessionHostLabel.ForAPeer(_project.Project, request.RequestId));
      receipt.RecordCliRequest(host.Id);
      DoneOnTheUIThread(() => AgentCli.Codex.CommandOfANewSession(null).HaveTheHubTypeItInto(host, Sandbox.HubOfTheSandbox.Hub, CancellationToken.None));
      HandedOver(_project);
      var tab = OnTheUIThread(() => Windows.Open.Single().Tabs.Single());
      WaitUntil(() => tab.SessionRunningInIt is not null, "the initial Codex session appeared");
      var initial = OnTheUIThread(() => tab.SessionRunningInIt!.Session.Id.Value);
      DoneOnTheUIThread(() => tab.Terminal.SendInputAsync("/new\r"));
      WaitUntil(() => tab.SessionRunningInIt?.Session.Id.Value is {} id && id != initial, "the same host showed a replacement session");
      using var retry = Posted("sessions/start", JsonContent.Create(request));
      retry.StatusCode.Must().Be(HttpStatusCode.Conflict);
      retry.Content.ReadAsStringAsync().GetAwaiter().GetResult().Must().Contain(host.Id.Value);
      OnTheUIThread(() => tab.SessionRunningInIt!.Session.Name).Must().BeNull();
      new PeerSessionLaunchReceipt(CloydsFolder, request).Session.Must().BeNull();
   }

   [XF] public void an_incompatible_hub_can_be_upgraded_then_the_same_launch_can_be_retried()
   {
      HandedOver(_project);
      var request = new SessionToStartForAPeer(Guid.NewGuid(), new string('D', 64), _project.Project.Folder, _worktree.Project.Folder, "codex", "After upgrade");
      var folder = Sandbox.HubOfTheSandbox.Environment["CLAUDE_TOOLING_LOCAL_API_FOLDER"];
      var file = new FileWrittenWhole(Path.Combine(folder, "local-api.json"));
      var original = file.Text()!;
      var announcement = System.Text.Json.Nodes.JsonNode.Parse(original)!.AsObject();
      announcement["protocol"] = -1;
      try
      {
         file.Write(announcement.ToJsonString());
         using var refusal = Posted("sessions/start", JsonContent.Create(request));
         refusal.StatusCode.Must().Be(HttpStatusCode.InternalServerError);
         new PeerSessionLaunchReceipt(CloydsFolder, request).HostWasRequested.Must().BeFalse();
      }
      finally { file.Write(original); }
      Start(request).Must().Satisfy(it => it.StartsWith("codex:"));
   }

   string Start(SessionToStartForAPeer request)
   {
      using var answer = Posted("sessions/start", JsonContent.Create(request));
      answer.EnsureSuccessStatusCode();
      return answer.Content.ReadFromJsonAsync<PeerSessionStarted>().GetAwaiter().GetResult()!.Address;
   }
}
