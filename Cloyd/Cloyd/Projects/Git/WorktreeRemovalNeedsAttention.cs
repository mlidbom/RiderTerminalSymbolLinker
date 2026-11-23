namespace Cloyd.Projects.Git;

/// <summary>Git has already unregistered a checkout. Keep the remaining work visible instead
/// of trying to inspect the removed checkout as if it were still registered.</summary>
sealed class WorktreeRemovalNeedsAttention(string message, Exception? cause = null) : InvalidOperationException(message, cause);
