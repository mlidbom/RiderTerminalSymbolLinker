using System.Diagnostics;

namespace Cloyd.Projects.Git;

/// <summary>Recognizes the registered .claude-shared worktrees of the machine's central catalog.</summary>
sealed class SharedRulesCatalog(Project machineClone)
{
   public static SharedRulesCatalog OnThisMachine { get; } = new(new Project(Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", ".claude-shared")));
   public Project MachineClone => machineClone;
   public static Project FolderIn(Project parent) => new(Path.Combine(parent.Folder, ".claude-shared"));

   public async Task<Project?> LinkedIn(Project parent, CancellationToken cancellation)
   {
      var nested = FolderIn(parent);
      if(!File.Exists(Path.Combine(nested.Folder, ".git"))) return null;
      if(File.GetAttributes(nested.Folder).HasFlag(FileAttributes.ReparsePoint)) return null;
      var registered = await GitWorktrees.Read(machineClone, cancellation);
      if(registered?.Repository.Equals(machineClone) != true || !registered.Checkouts.Any(it => it.Root.Equals(nested) && !it.IsMain)) return null;
      var list = await GitWorktrees.Read(nested, cancellation);
      if(list?.Repository.Equals(machineClone) != true) return null;
      var administration = (await GitCommand.In(nested, cancellation, "rev-parse", "--absolute-git-dir")).CheckedOutput().Trim();
      var backlink = Path.Combine(administration, "gitdir");
      if(!File.Exists(backlink) || !new Project(Path.GetFullPath(File.ReadAllText(backlink).Trim(), administration)).Equals(new Project(Path.Combine(nested.Folder, ".git"))))
         throw new InvalidOperationException($"The shared rules registration does not point back to {nested}.");
      return nested;
   }

   public async Task AttachIfUsed(Project repository, Project worktree, CancellationToken cancellation)
   {
      var hook = Path.Combine(worktree.Folder, ".claude", "attach-shared-claude-config.sessionstart-hook.sh");
      if(!File.Exists(hook) && !FolderIn(repository).Equals(machineClone) && await LinkedIn(repository, cancellation) is null) return;
      var script = Path.Combine(machineClone.Folder, "git-scripts", "sessionstart-hook.attach-the-catalog.sh");
      if(!File.Exists(script)) throw new InvalidOperationException($"Shared rules setup is missing at {script}.");
      var target = FolderIn(worktree);
      if(Path.Exists(target.Folder)) throw new InvalidOperationException($"Shared rules setup requires an unused path at {target}.");
      var home = Directory.GetParent(machineClone.Folder)!.Parent!.FullName;
      var shell = (await GitCommand.In(worktree, cancellation, "var", "GIT_SHELL_PATH")).CheckedOutput().Trim();
      var start = new ProcessStartInfo(shell, [script.Replace('\\','/'), worktree.Folder.Replace('\\','/')]) { WorkingDirectory = worktree.Folder };
      start.Environment["HOME"] = home;
      start.Environment["PATH"] = Path.GetDirectoryName(shell) + Path.PathSeparator + start.Environment["PATH"];
      var attached = await GitCommand.Run(start, cancellation);
      if(await LinkedIn(worktree, cancellation) is null)
         throw new InvalidOperationException($"Shared rules setup exited with {attached.ExitCode} without attaching {target}. {attached.Output.Trim()} {attached.Error.Trim()}");
   }
}
