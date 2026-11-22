using Cloyd.Chat.Writing;
using Cloyd.Codex.Chat;
using Cloyd.Hub;
using Cloyd.Pasting;
using Cloyd.Saving;

namespace Cloyd.Chat;

/// <summary>The chat input state for one hosted shell. Each window creates its own view of the same
/// Claude Code draft and delivery queue, or the Codex draft belonging to the selected Codex session.</summary>
sealed class SessionChatState
{
   readonly CodexSessionDrafts _codex = new();
   public ClaudeCodeChatState ClaudeCode { get; }
   readonly TheHub _hub;
   readonly PastedImages _images;

   public SessionChatState(TheHub hub, PastedImages images)
   {
      _hub = hub;
      _images = images;
      ClaudeCode = new ClaudeCodeChatState(hub);
   }

   public IReadOnlyList<PartOfAMessage> ClaudeCodeDraft => ClaudeCode.Input.AsSaved;
   public IReadOnlyList<SavedCodexDraft>? CodexDrafts => _codex.AsSaved;
   public ChatOfAClaudeCodeSession CreateClaudeCodeChat() => new(ClaudeCode, _images);
   public ChatOfACodexSession CreateCodexChat() => new(_hub, _images, _codex);

   public void Restore(SavedSessionTab saved)
   {
      ClaudeCode.Input.Draft.PutBackAhead(saved.Draft ?? []);
      _codex.Restore(saved.CodexDrafts ?? []);
   }
}
