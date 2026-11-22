using Cloyd.Projects;
using Cloyd.Projects.Git;
using Cloyd.Saving;

namespace Cloyd.ProjectsDashboard;

/// <summary>Git worktrees discovered from open windows, session hosts and previously seen repository roots.
/// A session host can work in a different checkout from the window its label names.</summary>
sealed class DashboardWorktrees
{
   Dictionary<Project, GitWorktrees?> _byFolder = [];
   HashSet<Project> _lastLiveFolders = [];
   DateTime _readAgain;
   bool _reading;

   public GitWorktrees? Of(Project folder) => _byFolder.GetValueOrDefault(folder);
   public IEnumerable<GitWorktrees> Repositories => _byFolder.Values.OfType<GitWorktrees>().DistinctBy(it => it.Repository);

   public async Task Discover(ProjectWindows windows, SavedDashboardArrangement saved, Action refreshed, Action<Exception> failed, CancellationToken cancellation)
   {
      var live = FoldersInUse(windows);
      if(_reading || (_lastLiveFolders.SetEquals(live) && DateTime.UtcNow < _readAgain)) return;
      _reading = true;
      try
      {
         var remembered = saved.Repositories.Select(it => new Project(it)).ToHashSet();
         var retry = new List<Project>();
         var readings = await Task.WhenAll(live.Union(remembered).Select(Read));
         if(cancellation.IsCancellationRequested) return;

         _byFolder = readings.ToDictionary(it => it.Folder, it => it.Repository);
         foreach(var repository in Repositories.ToArray())
            foreach(var checkout in repository.Checkouts) _byFolder.TryAdd(checkout.Root, repository);
         saved.Repositories = Repositories.Select(it => it.Repository).Union(retry).Select(it => it.Folder).ToList();
         _lastLiveFolders = live;
         _readAgain = DateTime.UtcNow.AddSeconds(15);
         refreshed();

         async Task<(Project Folder, GitWorktrees? Repository)> Read(Project folder)
         {
            GitWorktrees? repository;
            try { repository = await GitWorktrees.Read(folder, cancellation); }
            catch(OperationCanceledException) when(cancellation.IsCancellationRequested) { return (folder, null); }
            catch(Exception failure)
            {
               failed(failure);
               if(remembered.Contains(folder)) retry.Add(folder);
               repository = Of(folder);
            }

            // Git searches parent folders. A saved root alone names only its own repository, while a
            // current working folder legitimately discovers the repository that contains it.
            return (folder, live.Contains(folder) || Equals(repository?.Repository, folder) ? repository : null);
         }
      }
      finally { _reading = false; }
   }

   static HashSet<Project> FoldersInUse(ProjectWindows windows)
   {
      var folders = windows.Open.Select(it => it.Project).ToHashSet();
      foreach(var host in windows.Sessions.Hosts)
      {
         folders.Add(new Project(host.Directory));
         if(host.Project is {} project) folders.Add(project);
      }
      return folders;
   }
}
