using Cloyd.Chat.Writing;
using System.Text.Json.Serialization;

namespace Cloyd.Codex.Chat;

/// <summary>A steer that Chat keeps until Codex records its client message ID. The original
/// <see cref="MessageFromTheChat"/> can be restored with all its text and images if the turn ends first.</summary>
sealed record SubmittedCodexSteer(CodexTurnId Turn, string ClientUserMessageId, IReadOnlyList<PartOfAMessage> Parts)
{
   [JsonIgnore] public MessageFromTheChat Message => new(Parts);
   public static SubmittedCodexSteer For(CodexTurnId turn, MessageFromTheChat message) =>
      new(turn, Guid.CreateVersion7().ToString("D"), message.Parts);
}
