using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Hub;
using Cloyd.Hub.Worktrees;
using Cloyd.Projects;

namespace Cloyd.Saving;

/// <summary>The exact session disposition confirmed for one worktree. A lost hub reply or
/// Cloyd restart leaves this request available to replay under the same operation ID.</summary>
sealed class PendingWorktreeDisposition(CloydsFolder folder, Project worktree)
{
   readonly string _path = Path.Combine(folder.Folder.FullName, "worktree-dispositions",
      Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(worktree.Folder.ToUpperInvariant()))) + ".json");

   public async Task<bool> Reconcile(TheHub hub, Action<SessionHostId> forgetHost)
   {
      if(new FileWrittenWhole(_path).Text() is not {} saved) return false;
      var request = JsonSerializer.Deserialize<Request>(saved) ?? throw new InvalidDataException("Cloyd could not read the pending worktree disposition.");
      request.Preview.RequireReadyFor(worktree);
      await ObtainReceipt(hub, request, forgetHost);
      return true;
   }

   public Task<WorktreeSessionsResult> Apply(TheHub hub, WorktreeSessionsPreview preview, string? disposition, Action<SessionHostId> forgetHost)
   {
      var request = new Request(preview, disposition);
      new FileWrittenWhole(_path).Write(JsonSerializer.Serialize(request));
      return ObtainReceipt(hub, request, forgetHost);
   }

   async Task<WorktreeSessionsResult> ObtainReceipt(TheHub hub, Request request, Action<SessionHostId> forgetHost)
   {
      // Once Magnus confirmed, closing the window stops Git removal but still allows receipt reconciliation.
      using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
      var result = await hub.ApplyWorktreeDisposition(request.Preview, request.Disposition, timeout.Token);
      result.RecordConfirmedClosures(request.Preview, request.Disposition, forgetHost);
      if(result.Status is "blocked" or "partial") File.Delete(_path);
      result.RequireComplete(request.Preview);
      File.Delete(_path);
      return result;
   }

   sealed record Request(WorktreeSessionsPreview Preview, string? Disposition);
}
