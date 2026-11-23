using Cloyd.Hub;
using Cloyd.LocalApi;
using Cloyd.SessionHosts;
using Cloyd.Sessions;

namespace Cloyd.Projects;

sealed partial class ProjectWindow
{
   /// <summary>Starts or recovers a coordinator's helper in its worktree, keeping the current tab selected.
   /// Its address comes from the CLI observed in the host. Peers names a Codex session by this ID.</summary>
   public async Task<Session> SessionForAPeer(PeerSessionLaunchReceipt receipt)
   {
      await BringingBackTheTabs;
      using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_closed.Token);
      deadline.CancelAfter(TimeSpan.FromSeconds(90));
      deadline.Token.ThrowIfCancellationRequested();
      var request = receipt.Request;
      var session = request.Session;
      var launchHub = await _hub.ForOneGeneration(deadline.Token);
      SessionTab tab;
      if(receipt.HostWasRequested)
         tab = await RecoverHost();
      else
      {
         if(!(await launchHub.AgentClisItServes(deadline.Token)).Contains(session.AgentCli))
            throw new InvalidOperationException($"The hub does not serve {session.AgentCli}.");
         deadline.Token.ThrowIfCancellationRequested();
         receipt.RecordHostRequest();
         var host = await Sessions.StartPeerHost(launchHub, Project, request.Worktree, request.RequestId, deadline.Token);
         tab = ShowPeerHost(host);
      }

      deadline.Token.ThrowIfCancellationRequested();
      using var identity = new FirstSessionOfAPeerLaunch(tab.Session, request, receipt.Session);
      if(receipt.Session is null)
      {
         if(tab.Session.Running is not null) throw new InvalidOperationException($"Host {tab.Host.Id} already runs a session. No CLI was started.");
         deadline.Token.ThrowIfCancellationRequested();
         receipt.RecordCliRequest(tab.Host.Id);
         // Peers names the exact Codex ID before briefing it.
         var command = session.AgentCli == AgentCli.Codex ? AgentCli.Codex.CommandOfANewSession(null) : session.Command;
         try { await command.HaveTheHubTypeItInto(tab.Host, launchHub, deadline.Token); }
         catch(RefusedByTheHub)
         {
            receipt.RecordHostRequest(); // The hub explicitly refused to type into this shell.
            throw;
         }
      }

      return await identity.Confirm(deadline.Token);

      SessionTab ShowPeerHost(SessionHostOfTheHub host)
      {
         deadline.Token.ThrowIfCancellationRequested();
         var shown = Show(host, latestSession: null, session);
         _tabs.SelectedItem ??= shown;
         return shown;
      }

      async Task<SessionTab> RecoverHost()
      {
         if(Tabs.SingleOrDefault(it => Matches(it.Host)) is {} shown) return shown;
         var hosts = await launchHub.SessionHosts(deadline.Token);
         deadline.Token.ThrowIfCancellationRequested();
         if(hosts.SingleOrDefault(Matches) is {} original && Sessions.CanRestore(original)) return ShowPeerHost(original);
         throw new InvalidOperationException("The original launch has no available host. Inspect the project before starting another helper.");
      }

      bool Matches(SessionHostOfTheHub host) => SessionHostLabel.IdentifiesPeerRequest(host.Label, request.RequestId);
   }
}
