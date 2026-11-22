using Cloyd.LocalApi.Contract;
using Cloyd.Projects;

namespace Cloyd.LocalApi;

/// <summary>The UI thread admits one active start per request ID. The operation outlives its HTTP client.</summary>
sealed class SessionStartsForPeers(CloydsFolder folder, ProjectWindows windows)
{
   readonly Dictionary<Guid, (SessionToStartForAPeer Request, Task<PeerSessionStarted> Result)> _starting = [];

   public async Task<PeerSessionStarted> Start(SessionToStartForAPeer request)
   {
      var receipt = new PeerSessionLaunchReceipt(folder, request);
      if(_starting.TryGetValue(request.RequestId, out var inProgress))
      {
         receipt.RequireSameRequest(inProgress.Request);
         return await inProgress.Result;
      }
      if(receipt.Session is {} session) return session;
      var starting = Open();
      _starting.Add(request.RequestId, (request, starting));
      try { return await starting; }
      finally { _starting.Remove(request.RequestId); }

      async Task<PeerSessionStarted> Open()
      {
         var window = windows.ForAPeer(new Project(request.Project));
         return receipt.RecordSession(await window.SessionForAPeer(receipt));
      }
   }
}
