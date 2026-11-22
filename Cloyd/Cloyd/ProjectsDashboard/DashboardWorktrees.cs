using Cloyd.Projects;
using Cloyd.Projects.Git;
using Cloyd.Saving;

namespace Cloyd.ProjectsDashboard;

/// <summary>Git worktrees discovered from open windows, session hosts and previously seen repository roots.
/// A session host can work in a different checkout from the window its label names.</summary>
sealed class DashboardWorktrees
{
   Dictionary<Project, GitWorktrees?> _byFolder = [];
   HashSet<Project> _lastRequestedFolders = [];
   DateTime _readAgain;
   DateTime _readDormantAgain;
   public Task CurrentReading { get; private set; } = Task.CompletedTask;

   public GitWorktrees? Of(Project folder) => _byFolder.GetValueOrDefault(folder);
   public IEnumerable<GitWorktrees> Repositories => _byFolder.Values.OfType<GitWorktrees>().DistinctBy(it => it.Repository);
   public IReadOnlySet<Project> TrustedProjects { get; private set; } = new HashSet<Project>();

   public Task Discover(ProjectWindows windows, IEnumerable<Project> displayedFolders, SavedDashboardArrangement saved, Action refreshed, Action<Exception> failed, CancellationToken cancellation)
   {
      if(!CurrentReading.IsCompleted) return CurrentReading;
      var live = FoldersInUse(windows);
      var requested = live.Union(displayedFolders).ToHashSet();
      if(_lastRequestedFolders.SetEquals(requested) && DateTime.UtcNow < _readAgain) return Task.CompletedTask;
      return CurrentReading = ReadRequestedFolders();

      async Task ReadRequestedFolders()
      {
         var remembered = saved.Repositories.Select(it => new Project(it)).ToHashSet();
         var retry = new List<Project>();
         try
         {
            var home = await windows.Sessions.Hub.ClaudeCodeFolderItServes(cancellation);
            TrustedProjects = await Task.Run(home.TrustedProjects, cancellation);
         }
         catch(OperationCanceledException) when(cancellation.IsCancellationRequested) { return; }
         catch(Exception failure) { failed(failure); }
         var refreshDormant = DateTime.UtcNow >= _readDormantAgain;
         using var gitProcesses = new SemaphoreSlim(4);
         var pending = new List<Task<(Project Folder, GitWorktrees? Repository)>>();
         foreach(var folder in requested.Union(remembered).Union(TrustedProjects)) pending.Add(Read(folder, gitProcesses));
         var readings = await Task.WhenAll(pending);
         if(cancellation.IsCancellationRequested) return;

         _byFolder = readings.ToDictionary(it => it.Folder, it => it.Repository);
         foreach(var repository in Repositories.ToArray())
            foreach(var checkout in repository.Checkouts) _byFolder.TryAdd(checkout.Root, repository);
         saved.Repositories = readings.Where(it => live.Contains(it.Folder) || remembered.Contains(it.Folder))
                                      .Select(it => it.Repository?.Repository).OfType<Project>().Union(retry).Select(it => it.Folder).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
         _lastRequestedFolders = requested;
         _readAgain = DateTime.UtcNow.AddSeconds(15);
         if(refreshDormant) _readDormantAgain = DateTime.UtcNow.AddMinutes(2);
         refreshed();

         async Task<(Project Folder, GitWorktrees? Repository)> Read(Project folder, SemaphoreSlim available)
         {
            GitWorktrees? repository;
            try
            {
               if(!live.Contains(folder) && !refreshDormant && _byFolder.TryGetValue(folder, out var known)) repository = known;
               else
               {
                  await available.WaitAsync(cancellation);
                  try { repository = await Task.Run(() => GitWorktrees.Read(folder, cancellation), cancellation); }
                  finally { available.Release(); }
               }
            }
            catch(OperationCanceledException) when(cancellation.IsCancellationRequested) { return (folder, null); }
            catch(Exception failure)
            {
               failed(failure);
               if(remembered.Contains(folder)) retry.Add(folder);
               repository = Of(folder);
            }

            // Git searches parent folders. A saved root alone names only its own repository, while a
            // current working folder legitimately discovers the repository that contains it.
            return (folder, requested.Contains(folder) || TrustedProjects.Contains(folder) || Equals(repository?.Repository, folder) ? repository : null);
         }
      }
   }

   static HashSet<Project> FoldersInUse(ProjectWindows windows)
   {
      var folders = windows.Open.Select(it => it.Project).ToHashSet();
      folders.UnionWith(windows.Sessions.Running.Select(it => it.WorkingFolder).OfType<Project>());
      foreach(var host in windows.Sessions.Hosts)
      {
         folders.Add(new Project(host.Directory));
         if(host.Project is {} project) folders.Add(project);
      }
      return folders;
   }
}
