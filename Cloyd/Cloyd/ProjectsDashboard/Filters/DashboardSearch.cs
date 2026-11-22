namespace Cloyd.ProjectsDashboard.Filters;

/// <summary>Each search word can match the session, its worktree or its project.
/// Matching a heading also matches the sessions beneath that heading.</summary>
sealed class DashboardSearch(string text)
{
   readonly string[] _words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
   public bool IsActive => _words.Length != 0;

   public bool Matches(DashboardRow row) => _words.All(word => MatchesHereOrAbove(row, word));

   static bool MatchesHereOrAbove(DashboardRow row, string word)
   {
      for(DashboardRow? current = row; current is not null; current = current.Parent)
      {
         var matches = current switch
         {
            DashboardSession session => Contains(session.Name),
            DashboardWorktree worktree => Contains(worktree.Name) || Contains(worktree.Branch),
            DashboardGroup group => Contains(group.Name),
            _ => false
         };
         if(matches) return true;
      }
      return false;

      bool Contains(string? name) => name?.Contains(word, StringComparison.OrdinalIgnoreCase) == true;
   }
}
