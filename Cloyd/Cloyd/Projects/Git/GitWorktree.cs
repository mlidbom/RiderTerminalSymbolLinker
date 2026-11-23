namespace Cloyd.Projects.Git;

/// <summary>A working checkout registered with Git, including a checkout outside the main repository's folder.</summary>
sealed record GitWorktree(Project Root, string? Branch, bool IsMain, bool Locked = false)
{
   public string Name => IsMain ? Branch ?? Root.Name : Root.Name;
   public string Description => Branch is {} branch ? $"Branch {branch}" : "Detached HEAD";
}
