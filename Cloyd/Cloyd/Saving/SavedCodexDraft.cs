using Cloyd.Chat.Writing;
using Cloyd.Codex.Chat;
using System.Text.Json.Serialization;

namespace Cloyd.Saving;

/// <summary>An unsent Codex draft in a tab, including a message whose acknowledgement was lost.
/// A new tab can have a draft before its terminal has shown its session ID.</summary>
sealed record SavedCodexDraft(string? SessionId, IReadOnlyList<PartOfAMessage> Parts, string? Notice,
                             [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SubmittedCodexSteer>? PendingSteers = null);
