using System.Collections.Concurrent;
using Cloyd.Projects.Git;

namespace Cloyd.ProjectsDashboard;

/// <summary>Reads branch and working-tree state independently of hub activity. A repository shares one
/// integration-branch reading across its worktrees. Only a few Git processes run concurrently.</summary>
sealed class DashboardGitStatuses
{
   IReadOnlyDictionary<GitWorktree, WorktreeStatus> _statuses = new Dictionary<GitWorktree, WorktreeStatus>();
   DateTime _nextRead;
   bool _reading;

   public WorktreeStatus? Of(GitWorktree checkout) => _statuses.GetValueOrDefault(checkout);

   public async Task Refresh(IEnumerable<GitWorktrees> repositories, Action changed, CancellationToken cancellation)
   {
      if(_reading || cancellation.IsCancellationRequested) return;
      var all = repositories.ToArray();
      if(all.Length == 0 || DateTime.UtcNow < _nextRead && all.SelectMany(it => it.Checkouts).All(_statuses.ContainsKey)) return;
      _reading = true;
      try
      {
         var results = new ConcurrentDictionary<GitWorktree, WorktreeStatus>();
         await Parallel.ForEachAsync(all, new ParallelOptions { MaxDegreeOfParallelism = 3, CancellationToken = cancellation },
                                     (repository, token) => ReadRepository(repository, results, token));
         cancellation.ThrowIfCancellationRequested();
         _statuses = results;
         _nextRead = DateTime.UtcNow.AddSeconds(30);
         changed();
      }
      finally { _reading = false; }
   }

   static async ValueTask ReadRepository(GitWorktrees repository, ConcurrentDictionary<GitWorktree, WorktreeStatus> results, CancellationToken cancellation)
   {
      GitIntegrationBranch? target;
      try { target = await GitIntegrationBranch.Of(repository.Repository, cancellation); }
      catch(Exception failure) when(!cancellation.IsCancellationRequested)
      {
         foreach(var checkout in repository.Checkouts) results[checkout] = WorktreeStatus.Unavailable(failure);
         return;
      }

      foreach(var checkout in repository.Checkouts)
      {
         try { results[checkout] = await WorktreeStatus.Read(checkout, target, cancellation); }
         catch(Exception failure) when(!cancellation.IsCancellationRequested) { results[checkout] = WorktreeStatus.Unavailable(failure); }
      }
   }
}
