namespace Cloyd.Projects.Git;

/// <summary>Evidence from the branches origin currently advertises for the reviewed commit.</summary>
sealed record BranchOnOrigin(string Description, bool Verified = false)
{
   public static async Task<BranchOnOrigin> Read(Project root, string? commit, string? branch, CancellationToken cancellation)
   {
      if(commit is null or "(initial)") return new("No commit");
      var remotes = (await GitCommand.In(root, cancellation, "remote")).CheckedOutput().Split('\n', StringSplitOptions.TrimEntries);
      if(!remotes.Contains("origin")) return new("No origin configured");
      var address = (await GitCommand.In(root, cancellation, "remote", "get-url", "origin")).CheckedOutput().Trim();
      var localPath = Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri.IsFile ? uri.LocalPath : null :
         address.Contains(':') ? null : Path.GetFullPath(address, root.Folder);
      if(localPath is not null && Directory.Exists(localPath))
      {
         var local = new Project(localPath);
         if(root.Contains(local)) return new("Origin is inside this worktree");
         var originGit = await GitCommand.In(local, cancellation, "rev-parse", "--path-format=absolute", "--git-common-dir");
         var ownGit = (await GitCommand.In(root, cancellation, "rev-parse", "--path-format=absolute", "--git-common-dir")).CheckedOutput();
         if(originGit.ExitCode == 0 && new Project(originGit.Output.Trim()).Equals(new Project(ownGit.Trim()))) return new("Origin is the same local repository");
      }

      using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
      deadline.CancelAfter(TimeSpan.FromSeconds(10));
      GitCommand advertised;
      try { advertised = await GitCommand.In(root, deadline.Token, "ls-remote", "--heads", "origin"); }
      catch(OperationCanceledException) when(!cancellation.IsCancellationRequested) { return new("Origin verification timed out"); }
      if(advertised.ExitCode != 0) return new("Could not verify origin");
      var heads = advertised.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
         .Select(it => it.Split('\t')).Select(it => (Tip: it[0], Branch: it[1]["refs/heads/".Length..]))
         .OrderByDescending(it => it.Branch == branch).ToArray();

      foreach(var head in heads)
         if(head.Tip == commit) return new("origin/" + head.Branch, true);
      var unknown = false;
      foreach(var head in heads.DistinctBy(it => it.Tip))
      {
         var ancestry = await GitCommand.In(root, cancellation, "merge-base", "--is-ancestor", commit, head.Tip);
         if(ancestry.ExitCode == 0) return new("origin/" + head.Branch, true);
         unknown |= ancestry.ExitCode != 1;
      }
      var shallow = (await GitCommand.In(root, cancellation, "rev-parse", "--is-shallow-repository")).CheckedOutput().Trim() == "true";
      return new(unknown || (shallow && heads.Length > 0) ? "Origin ancestry unverified" : "Tip absent from origin branches");
   }
}
