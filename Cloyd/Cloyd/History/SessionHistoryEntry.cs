using System.Globalization;
using Cloyd.Sessions;

namespace Cloyd.History;

/// <summary>
/// A session of the <see cref="SessionHistory"/>, of any agent CLI, as the <see cref="SessionHistoryPicker"/> lists it:
/// what the hub tells of it on disk, and <see cref="Where"/> it is now.
/// </summary>
sealed class SessionHistoryEntry
{
   readonly SessionListing _listing;

   /// <summary>The entry of <paramref name="listing"/>, which is <paramref name="where"/> now.</summary>
   public SessionHistoryEntry(SessionListing listing, WhereTheSessionIs where)
   {
      _listing = listing;
      Where = where;
   }

   /// <summary>The session, named as its agent CLI shows it, as a tab that resumes it is titled.</summary>
   public Session Session => _listing.Session;

   public SessionId Id => Session.Id;

   /// <summary>The agent CLI the session is one of, which the picker marks each entry with, and lists or leaves out.</summary>
   public AgentCli AgentCli => Session.AgentCli;

   /// <summary>Where the session is now, which decides what choosing it does.</summary>
   public WhereTheSessionIs Where { get; }

   /// <summary>What the picker calls the session: its name, else its <see cref="Prompt"/>, else its id.</summary>
   public string Title => _listing.Title;

   /// <summary>
   /// The session's prompt, which the picker shows below a <see cref="Title"/> that is the session's name: its last, or a
   /// Codex session's first, as Codex previews a session. Null otherwise.
   /// </summary>
   public string? Prompt => _listing.Prompt;

   /// <summary>
   /// The directory the session last ran in: null when the hub can't tell, as for a Claude Code transcript none of whose
   /// entries name a git branch, which the hub takes the directory from. Only <see cref="SessionHistoryScope.AllProjects"/> covers
   /// such a session.
   /// </summary>
   public string? Directory => _listing.Directory;

   /// <summary>The git branch checked out in <see cref="Directory"/> as the session last ran: null outside git.</summary>
   public string? Branch => _listing.Branch;

   /// <summary>
   /// When the session was last used, as the picker tells it <paramref name="now"/>, in local time: <c>today 14:22</c>,
   /// <c>yesterday 09:05</c>, <c>12 Nov 14:22</c> this year, else <c>12 Nov 2025</c>.
   /// </summary>
   public string LastUsedAsToldAt(DateTime now)
   {
      if(_listing.Recorded is not {} recorded) return "Running now";
      var used = recorded.LastUsed.ToLocalTime();
      var today = now.ToLocalTime().Date;
      return used.Date == today ? $"today {used:HH:mm}"
         : used.Date == today.AddDays(-1) ? $"yesterday {used:HH:mm}"
         : used.Year == today.Year ? used.ToString("d MMM HH:mm", CultureInfo.InvariantCulture)
         : used.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
   }

   /// <summary>
   /// Whether every word of <paramref name="words"/> is in what the picker shows of the session, in any case: its name,
   /// its prompt, its directory and its branch. An empty filter has no words, so every entry has them.
   /// </summary>
   public bool Matches(IReadOnlyList<string> words)
   {
      foreach(var word in words)
         if(!Contains(Title, word) && !Contains(Prompt, word) && !Contains(Directory, word) && !Contains(Branch, word)) return false;
      return true;

      static bool Contains(string? text, string word) => text?.Contains(word, StringComparison.OrdinalIgnoreCase) == true;
   }

   /// <summary>Does what choosing the entry in the picker of <paramref name="pickedIn"/> does, as <see cref="Where"/> it is decides.</summary>
   public Task Choose(ISessionHistoryTarget pickedIn) => Where.Choose(Session, pickedIn);
}
