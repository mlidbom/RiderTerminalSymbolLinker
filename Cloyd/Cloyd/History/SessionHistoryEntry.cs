using System.Globalization;
using Cloyd.Hub;
using Cloyd.Sessions;

namespace Cloyd.History;

/// <summary>
/// A session of the <see cref="SessionHistory"/>, of any agent CLI, as the <see cref="SessionHistoryPicker"/> lists it:
/// what the hub tells of it on disk, and <see cref="Where"/> it is now.
/// </summary>
sealed class SessionHistoryEntry
{
   readonly SessionOnDisk _onDisk;

   /// <summary>The entry of <paramref name="onDisk"/>, which is <paramref name="where"/> now.</summary>
   public SessionHistoryEntry(SessionOnDisk onDisk, WhereTheSessionIs where)
   {
      _onDisk = onDisk;
      Where = where;
   }

   /// <summary>The session, named as its agent CLI shows it, as a tab that resumes it is titled.</summary>
   public Session Session => _onDisk.Session;

   public SessionId Id => Session.Id;

   /// <summary>The agent CLI the session is one of, which the picker marks each entry with, and lists or leaves out.</summary>
   public AgentCli AgentCli => Session.AgentCli;

   /// <summary>Where the session is now, which decides what choosing it does.</summary>
   public WhereTheSessionIs Where { get; }

   /// <summary>What the picker calls the session: its name, else its <see cref="Prompt"/>, else its id.</summary>
   public string Title => Session.Name ?? ThePrompt ?? Id.Value;

   /// <summary>
   /// The session's prompt, which the picker shows below a <see cref="Title"/> that is the session's name: its last, or a
   /// Codex session's first, as Codex previews a session. Null otherwise.
   /// </summary>
   public string? Prompt => Session.Name is null ? null : ThePrompt;

   string? ThePrompt => _onDisk.LastPrompt ?? _onDisk.FirstPrompt;

   /// <summary>
   /// The directory the session last ran in: null when the hub can't tell, as for a Claude Code transcript none of whose
   /// entries name a git branch, which the hub takes the directory from. Only <see cref="SessionHistoryScope.AllProjects"/> covers
   /// such a session.
   /// </summary>
   public string? Directory => _onDisk.Directory;

   /// <summary>The git branch checked out in <see cref="Directory"/> as the session last ran: null outside git.</summary>
   public string? Branch => _onDisk.Branch;

   /// <summary>When the session was last written to, in UTC: a Claude Code session's transcript, or a Codex session by Codex.</summary>
   public DateTime LastUsed => _onDisk.LastUsed;

   /// <summary>
   /// When the session was last used, as the picker tells it <paramref name="now"/>, in local time: <c>today 14:22</c>,
   /// <c>yesterday 09:05</c>, <c>12 Nov 14:22</c> this year, else <c>12 Nov 2025</c>.
   /// </summary>
   public string LastUsedAsToldAt(DateTime now)
   {
      var used = LastUsed.ToLocalTime();
      var today = now.ToLocalTime().Date;
      return used.Date == today ? $"today {used:HH:mm}"
         : used.Date == today.AddDays(-1) ? $"yesterday {used:HH:mm}"
         : used.Year == today.Year ? used.ToString("d MMM HH:mm", CultureInfo.InvariantCulture)
         : used.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
   }

   /// <summary>
   /// Whether every word of <paramref name="filter"/> is in what the picker shows of the session, in any case: its name,
   /// its prompt, its directory and its branch. An empty filter has no words, so every entry has them.
   /// </summary>
   public bool Matches(string filter)
   {
      var shown = string.Join('\n', new[] { Session.Name, ThePrompt, Directory, Branch }.OfType<string>());
      return filter.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).All(word => shown.Contains(word, StringComparison.OrdinalIgnoreCase));
   }

   /// <summary>Does what choosing the entry in the picker of <paramref name="pickedIn"/> does, as <see cref="Where"/> it is decides.</summary>
   public Task Choose(ISessionHistoryTarget pickedIn) => Where.Choose(Session, pickedIn);
}
