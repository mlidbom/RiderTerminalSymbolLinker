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
      var request = receipt.Request;
      var session = request.Session;
      var launchHub = await _hub.ForOnePeerLaunch(deadline.Token);
      SessionTab tab;
      if(receipt.HostWasRequested)
         tab = await RecoverHost();
      else
      {
         if(!(await launchHub.AgentClisItServes(deadline.Token)).Contains(session.AgentCli))
            throw new InvalidOperationException($"The hub does not serve {session.AgentCli}.");
         receipt.RecordHostRequest();
         var host = await launchHub.StartASessionHost(_shellOfNewSessions, request.Worktree, SessionHostLabel.ForAPeer(Project, request.RequestId), deadline.Token);
         tab = ShowPeerHost(host);
      }

      if(receipt.Session is null)
      {
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

      while(true)
      {
         if(tab.SessionRunningInIt?.Session is {} running)
         {
            if(running.AgentCli != session.AgentCli || (receipt.Session is {} known && known.Address != $"{request.AgentCli}:{running.Id.Value}"))
               throw new InvalidOperationException($"The original helper {receipt.Session?.Address ?? session.Name} is no longer in host {tab.Host.Id}. Inspect the tab and history.");
            if(receipt.Session is not null || running.AgentCli == AgentCli.Codex || running.Name == session.Name) return running;
            if(!string.IsNullOrEmpty(running.Name))
               throw new InvalidOperationException($"The helper's host now shows {running.Name} ({running.Id.Value}), expected {session.Name}. Inspect the tab before sending the brief.");
         }
         await Task.Delay(150, deadline.Token);
      }

      SessionTab ShowPeerHost(SessionHostOfTheHub host)
      {
         var shown = Show(host, latestSession: null, session);
         _tabs.SelectedItem ??= shown;
         return shown;
      }

      async Task<SessionTab> RecoverHost()
      {
         if(Tabs.SingleOrDefault(it => Matches(it.Host)) is {} shown) return shown;
         var hosts = await launchHub.SessionHosts(deadline.Token);
         if(hosts.SingleOrDefault(Matches) is { Attached: false } original) return ShowPeerHost(original);
         throw new InvalidOperationException("The original launch has no available host. Inspect the project before starting another helper.");
      }

      bool Matches(SessionHostOfTheHub host) => SessionHostLabel.IdentifiesPeerRequest(host.Label, request.RequestId);
   }
}
