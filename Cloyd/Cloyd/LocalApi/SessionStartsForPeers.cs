using System.Text.Json;
using Cloyd.LocalApi.Contract;
using Cloyd.Projects;

namespace Cloyd.LocalApi;

/// <summary>Remembers the tab belonging to a launch. The UI thread admits one start per request ID.
/// A lost HTTP response leaves the operation running. A restarted Cloyd can find the surviving host by its label.</summary>
sealed class SessionStartsForPeers(CloydsFolder folder, ProjectWindows windows)
{
   readonly Dictionary<Guid, Task<PeerSessionStarted>> _starting = [];

   public async Task<PeerSessionStarted> Start(SessionToStartForAPeer request)
   {
      var receipt = new FileWrittenWhole(Path.Combine(folder.Folder.FullName, "peer-session-starts", $"{request.RequestId:D}.json"));
      var saved = receipt.Text() is {} text ? JsonSerializer.Deserialize<Receipt>(text)! : null;
      if(saved is not null && saved.Request != request)
         throw new ArgumentException("This request ID already belongs to a different launch. Retry with the original arguments.");
      if(saved?.Session is {} session) return session;
      if(_starting.TryGetValue(request.RequestId, out var inProgress)) return await inProgress;

      if(saved is null) receipt.Write(JsonSerializer.Serialize(new Receipt(request, null)));
      var starting = StartOrFindTheTab();
      _starting.Add(request.RequestId, starting);
      try { return await starting; }
      finally { _starting.Remove(request.RequestId); }

      async Task<PeerSessionStarted> StartOrFindTheTab()
      {
         var window = windows.ForAPeer(new Project(request.Project));
         var started = await window.SessionForAPeer(request, create: saved is null);
         var answer = new PeerSessionStarted($"{request.AgentCli}:{started.Id.Value}");
         receipt.Write(JsonSerializer.Serialize(new Receipt(request, answer)));
         return answer;
      }
   }

   sealed record Receipt(SessionToStartForAPeer Request, PeerSessionStarted? Session);
}
