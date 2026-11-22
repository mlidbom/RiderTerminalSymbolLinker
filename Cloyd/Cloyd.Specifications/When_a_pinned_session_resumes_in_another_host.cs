using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Codex;
using Cloyd.Hub;
using Cloyd.Hub.RunningSessions;
using Cloyd.Projects;
using Cloyd.ProjectsDashboard;
using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

public class When_a_pinned_session_resumes_in_another_host
{
   [XF] public void a_pin_follows_the_session_without_revealing_the_old_shell_or_pinning_its_next_agent()
   {
      var folder = new Project(@"C:\Dev\Example");
      var original = new SessionHostOfTheHub(SessionHostId.New(), 0, folder.Folder, null, false);
      var replacement = original with { Id = SessionHostId.New() };
      var oldTab = new DashboardSession($"host:{original.Id}");
      var newTab = new DashboardSession($"host:{replacement.Id}");
      var id = new CodexSessionId("the-pinned-session");
      var agent = new RunningCodex(id, "First title", original.Id, new StatusOfACodex("idle"));
      HashSet<string> pins = [oldTab.Key];
      oldTab.Update(original, agent, agent.Session, AgentCli.Codex, folder);
      oldTab.ReadPin(pins, new HashSet<SessionId> { id }).Must().BeTrue();
      oldTab.Update(original, null, agent.Session, AgentCli.Codex, folder);
      var resumed = agent with { SessionHost = replacement.Id, Name = "Renamed" };
      newTab.Update(replacement, resumed, resumed.Session, AgentCli.Codex, folder);
      var running = new HashSet<SessionId> { id };
      oldTab.ReadPin(pins, running);
      newTab.ReadPin(pins, running);
      newTab.MatchesPinFilter.Must().BeTrue();
      oldTab.MatchesPinFilter.Must().BeFalse();

      var next = agent with { SessionId = new CodexSessionId("another-session") };
      oldTab.Update(original, next, next.Session, AgentCli.Codex, folder);
      oldTab.ReadPin(pins, running);
      oldTab.IsPinned.Must().BeFalse();
   }
}
