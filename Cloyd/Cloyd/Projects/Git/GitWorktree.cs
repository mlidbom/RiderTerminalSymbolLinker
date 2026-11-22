namespace Cloyd.Projects.Git;

/// <summary>A working checkout registered with Git, including a checkout outside the main repository's folder.</summary>
sealed record GitWorktree(Project Root, string? Branch, bool IsMain)
{
   public string Name => IsMain ? Branch ?? Root.Name : Root.Name;
   public string Description => $"{Branch ?? "Detached HEAD"}{Environment.NewLine}{Root.Folder}";
}
