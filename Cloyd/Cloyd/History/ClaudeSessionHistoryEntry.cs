using System.Globalization;
using Cloyd.ClaudeCode;
using Cloyd.Hub;
using Cloyd.Projects;

namespace Cloyd.History;

/// <summary>
/// A session of the <see cref="ClaudeSessionHistory"/>, as the <see cref="ClaudeSessionHistoryPicker"/> lists it: what
/// its transcript tells of it, and <see cref="Where"/> it is now.
/// </summary>
sealed class ClaudeSessionHistoryEntry
{
   readonly TranscribedSession _transcribed;

   /// <summary>The entry of <paramref name="transcribed"/>, which is <paramref name="where"/> now.</summary>
   public ClaudeSessionHistoryEntry(TranscribedSession transcribed, WhereTheSessionIs where)
   {
      _transcribed = transcribed;
      Where = where;
   }

   public ClaudeCodeSessionId Id => _transcribed.Id;

   /// <summary>The session, named as Claude Code shows it, as a tab that resumes it is titled.</summary>
   public ClaudeCodeSession Session => new(Id, _transcribed.Title);

   /// <summary>Where the session is now, which decides what choosing it does.</summary>
   public WhereTheSessionIs Where { get; }

   /// <summary>What the picker calls the session: its title, else its last prompt, else its id.</summary>
   public string Title => _transcribed.Title ?? _transcribed.LastPrompt ?? Id.Value;

   /// <summary>The session's last prompt, which the picker shows below a <see cref="Title"/> that is the session's title: null otherwise.</summary>
   public string? LastPrompt => _transcribed.Title is null ? null : _transcribed.LastPrompt;

   /// <summary>
   /// The directory the session last ran in: null when the hub can't tell, as for a transcript none of whose entries name
   /// a git branch, which the hub takes the directory from. Only <see cref="ClaudeSessionHistoryScope.AllProjects"/> covers
   /// such a session.
   /// </summary>
   public string? Directory => _transcribed.Directory;

   /// <summary>The git branch checked out in <see cref="Directory"/> as the session last ran: null outside git.</summary>
   public string? Branch => _transcribed.Branch;

   /// <summary>When Claude Code last wrote the session's transcript, in UTC.</summary>
   public DateTime LastUsed => _transcribed.LastUsed;

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
   /// Whether every word of <paramref name="filter"/> is in what the picker shows of the session, in any case: its title,
   /// its last prompt, its directory and its branch. An empty filter has no words, so every entry has them.
   /// </summary>
   public bool Matches(string filter)
   {
      var shown = string.Join('\n', new[] { _transcribed.Title, _transcribed.LastPrompt, Directory, Branch }.OfType<string>());
      return filter.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).All(word => shown.Contains(word, StringComparison.OrdinalIgnoreCase));
   }

   /// <summary>Does what choosing the entry in the picker of <paramref name="pickedIn"/> does, as <see cref="Where"/> it is decides.</summary>
   public Task Choose(ProjectWindow pickedIn) => Where.Choose(Session, pickedIn);
}
