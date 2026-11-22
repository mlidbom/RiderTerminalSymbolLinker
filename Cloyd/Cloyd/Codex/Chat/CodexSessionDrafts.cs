using Cloyd.Saving;

namespace Cloyd.Codex.Chat;

/// <summary>The drafts of the Codex sessions this terminal has shown. Text typed before the first ID
/// appears belongs to that first session.</summary>
sealed class CodexSessionDrafts
{
   readonly Dictionary<CodexSessionId, CodexSessionDraft> _bySession = [];
   CodexSessionDraft _beforeTheSessionIsKnown = new();

   public CodexSessionDraft For(CodexSessionId? session)
   {
      if(session is null) return _beforeTheSessionIsKnown;
      if(_bySession.TryGetValue(session, out var draft)) return draft;
      _bySession.Add(session, draft = _beforeTheSessionIsKnown);
      _beforeTheSessionIsKnown = new();
      return draft;
   }

   public IReadOnlyList<SavedCodexDraft>? AsSaved
   {
      get
      {
         var drafts = _bySession.Select(it => it.Value.AsSaved(it.Key)).Append(_beforeTheSessionIsKnown.AsSaved(null)).OfType<SavedCodexDraft>().ToArray();
         return drafts.Length > 0 ? drafts : null;
      }
   }

   public void Restore(IReadOnlyList<SavedCodexDraft> drafts)
   {
      foreach(var draft in drafts) For(draft.SessionId is {} id ? new CodexSessionId(id) : null).Restore(draft);
   }
}
