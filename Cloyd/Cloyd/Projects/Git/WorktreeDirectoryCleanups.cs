using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Cloyd.Hub;
using Cloyd.Hub.Worktrees;

namespace Cloyd.Projects.Git;

/// <summary>Delivers confirmed worktree-directory cleanups to the hub. The hub owns identity checks,
/// scheduled deletion and needs-review jobs. Cloyd keeps only requests whose delivery might be interrupted.</summary>
sealed class WorktreeDirectoryCleanups(TheHub hub, CloydsFolder folder)
{
   readonly string _directory = Path.Combine(folder.Folder.FullName, "directory-cleanup-handoffs");
   readonly ConcurrentDictionary<Guid, byte> _inProgress = new();
   sealed record Handoff(Guid CleanupId, string WorktreePath, string RepositoryPath, string? Branch = null, string? Tip = null, GitProcess? Git = null);
   sealed record GitProcess(int Id, DateTime Started)
   {
      public bool StillRuns()
      {
         try
         {
            using var process = Process.GetProcessById(Id);
            return !process.HasExited && process.StartTime.ToUniversalTime() == Started;
         }
         catch(ArgumentException) { return false; } // GetProcessById reports an exited process this way.
      }
   }

   public Task<WorktreeDirectoryCleanup?> Prepare(Project worktree, CancellationToken cancellation) => hub.PrepareDirectoryCleanup(worktree, cancellation);

   public async Task<WorktreeDirectoryCleanup?> Remove(WorktreeRemoval checkout, WorktreeDirectoryCleanup ticket, bool deleteBranch, CancellationToken cancellation)
   {
      var root = checkout.Checkout.Root;
      ticket.Require(root, null, "prepared");
      var handoff = new Handoff(ticket.CleanupId, root.Folder, checkout.Repository.Folder, deleteBranch ? checkout.Checkout.Branch : null, checkout.Commit);
      if(!_inProgress.TryAdd(ticket.CleanupId, 0)) throw new InvalidDataException("The hub reused a directory cleanup ID.");
      try
      {
         cancellation.ThrowIfCancellationRequested();
         var record = new FileWrittenWhole(FileOf(ticket.CleanupId));
         record.Write(JsonSerializer.Serialize(handoff));
         var result = await checkout.RemoveDirectoryWithGit(process =>
            record.Write(JsonSerializer.Serialize(handoff with { Git = new GitProcess(process.Id, process.StartTime.ToUniversalTime()) })));
         WorktreeDirectoryCleanup? receipt = null;
         if(result.ExitCode != 0 || Path.Exists(root.Folder)) receipt = await ResolveGitResult(result, handoff);
         try
         {
            if(receipt is { Status: "needsReview" })
               throw new WorktreeRemovalNeedsAttention($"{root} was unregistered. {receipt.Reason} {result.Error.Trim()}{KeptBranch(handoff)}");
            if(deleteBranch) await checkout.DeleteReviewedBranch(receipt is { Status: "pending" } ? root.Folder : null);
            return receipt;
         }
         finally { File.Delete(FileOf(ticket.CleanupId)); }
      }
      finally { _inProgress.TryRemove(ticket.CleanupId, out _); }
   }

   async Task<WorktreeDirectoryCleanup> ResolveGitResult(GitCommand git, Handoff handoff)
   {
      bool registered;
      try { registered = await StillRegistered(handoff, CancellationToken.None); }
      catch(Exception failure)
      {
         throw new WorktreeRemovalNeedsAttention($"Could not verify the registration of {handoff.WorktreePath} after Git ran. Cloyd saved its cleanup request for reconciliation.{KeptBranch(handoff)} {failure.Message}", failure);
      }
      if(registered)
      {
         File.Delete(FileOf(handoff.CleanupId));
         git.CheckedOutput();
         throw new InvalidOperationException($"Git still lists {handoff.WorktreePath} as a registered checkout.");
      }
      try { return await Deliver(handoff, CancellationToken.None); }
      catch(Exception failure)
      {
         throw new WorktreeRemovalNeedsAttention($"{handoff.WorktreePath} was unregistered. Cloyd saved the directory cleanup request and will retry delivery.{KeptBranch(handoff)} {failure.Message}", failure);
      }
   }

   static string KeptBranch(Handoff handoff) => handoff.Branch is null ? "" : $" Kept local branch {handoff.Branch}, reviewed at {handoff.Tip}.";

   public async Task<string?> CleanupBlocker(Project root, CancellationToken cancellation)
   {
      if(Directory.Exists(_directory))
         foreach(var path in Directory.GetFiles(_directory, "*.json"))
            if(Read(path) is {} request && new Project(request.WorktreePath).Equals(root)) return $"Cloyd is reconciling directory cleanup for {root}.";
      if(!Path.Exists(root.Folder)) return null;
      return await hub.OutstandingDirectoryCleanup(root, cancellation) switch
      {
         { Status: "pending" } => $"Waiting for Windows to release the empty directory {root}. Cleanup is queued.",
         {} job => $"Directory cleanup needs review for {root}. {job.Reason}",
         null => null
      };
   }

   public async Task RecoverWhileOpen(CancellationToken cancellation, Action<Exception> report)
   {
      using var timer = new PeriodicTimer(TimeSpan.FromMinutes(3));
      var reported = new HashSet<Guid>();
      try
      {
         do
         {
            try
            {
               var problems = new List<Exception>();
               await Recover(cancellation, problems.Add);
               foreach(var job in await hub.OutstandingDirectoryCleanups(cancellation))
                  if(job.Status == "needsReview" && reported.Add(job.CleanupId))
                     problems.Add(new InvalidOperationException($"Directory cleanup needs review for {job.WorktreePath}. {job.Reason}"));
               if(problems.Count > 0) report(new InvalidOperationException(string.Join(Environment.NewLine, problems.Select(it => it.Message).Distinct())));
            }
            catch(Exception failure) when(!cancellation.IsCancellationRequested) { report(failure); }
         } while(await timer.WaitForNextTickAsync(cancellation));
      }
      catch(OperationCanceledException) when(cancellation.IsCancellationRequested) { }
   }

   /// <summary>After a crash or lost response, replay only directory delivery. Git and session
   /// disposition remain under the user's original foreground operation.</summary>
   public async Task Recover(CancellationToken cancellation, Action<Exception> report)
   {
      if(!Directory.Exists(_directory)) return;
      foreach(var path in Directory.GetFiles(_directory, "*.json"))
      {
         cancellation.ThrowIfCancellationRequested();
         if(!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out var id))
         {
            report(new InvalidDataException($"Unexpected directory cleanup handoff file: {path}"));
            continue;
         }
         if(!_inProgress.TryAdd(id, 0)) continue;
         try
         {
            if(Read(path) is not {} handoff) continue;
            if(handoff.CleanupId != id) throw new InvalidDataException("Directory cleanup handoff ID mismatch.");
            if(handoff.Git?.StillRuns() == true) continue;
            var receipt = await Deliver(handoff, cancellation);
            File.Delete(path);
            if(receipt.Status == "needsReview") report(new InvalidOperationException($"Directory cleanup needs review for {receipt.WorktreePath}. {receipt.Reason}"));
            if(handoff.Branch is {} branch) report(new InvalidOperationException($"Recovered directory cleanup for {handoff.WorktreePath}. Check local branch {branch} (reviewed at {handoff.Tip}); branch deletion is not replayed."));
         }
         catch(Exception failure) when(!cancellation.IsCancellationRequested) { report(new IOException($"Could not deliver directory cleanup {id}. {failure.Message}", failure)); }
         finally { _inProgress.TryRemove(id, out _); }
      }
   }

   static Handoff? Read(string path) => new FileWrittenWhole(path).Text() is {} json
      ? JsonSerializer.Deserialize<Handoff>(json) ?? throw new InvalidDataException("Empty directory cleanup handoff.") : null;

   static async Task<bool> StillRegistered(Handoff handoff, CancellationToken cancellation)
   {
      var repository = new Project(handoff.RepositoryPath);
      var listing = await GitWorktrees.Read(repository, cancellation);
      if(listing is null || !listing.Repository.Equals(repository)) throw new InvalidOperationException($"Cannot verify the worktree registration in {repository}.");
      return listing.RegisteredRoots.Contains(new Project(handoff.WorktreePath));
   }

   async Task<WorktreeDirectoryCleanup> Deliver(Handoff handoff, CancellationToken cancellation)
   {
      using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
      timeout.CancelAfter(TimeSpan.FromSeconds(30));
      return await hub.EnqueueDirectoryCleanup(new Project(handoff.WorktreePath), handoff.CleanupId, timeout.Token);
   }

   string FileOf(Guid id) => Path.Combine(_directory, id.ToString("N") + ".json");
}
