using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

   public async Task<bool> Reconcile(TheHub hub)
   {
      if(new FileWrittenWhole(_path).Text() is not {} saved) return false;
      var request = JsonSerializer.Deserialize<Request>(saved) ?? throw new InvalidDataException("Cloyd could not read the pending worktree disposition.");
      if(request.Preview.HasOpenTabsOrRunningSessions)
      {
         // An older confirmation could include closing a host. Retain that record and ask for a fresh preview.
         File.Move(_path, Path.ChangeExtension(_path, $"{request.Preview.OperationId:N}.superseded"), overwrite: true);
         return true;
      }
      request.Preview.RequireReadyFor(worktree);
      await ObtainReceipt(hub, request);
      return true;
   }

   public Task<WorktreeSessionsResult> Apply(TheHub hub, WorktreeSessionsPreview preview, string? disposition)
   {
      preview.RequireReadyFor(worktree);
      var request = new Request(preview, disposition);
      new FileWrittenWhole(_path).Write(JsonSerializer.Serialize(request));
      return ObtainReceipt(hub, request);
   }

   async Task<WorktreeSessionsResult> ObtainReceipt(TheHub hub, Request request)
   {
      // Once Magnus confirmed, closing the window stops Git removal but still allows receipt reconciliation.
      using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
      var result = await hub.ApplyWorktreeDisposition(request.Preview, request.Disposition, timeout.Token);
      result.ValidateReceipts(request.Preview, request.Disposition);
      if(result.Status is "blocked" or "partial") File.Delete(_path);
      result.RequireComplete(request.Preview);
      File.Delete(_path);
      return result;
   }

   sealed record Request(WorktreeSessionsPreview Preview, string? Disposition);
}
