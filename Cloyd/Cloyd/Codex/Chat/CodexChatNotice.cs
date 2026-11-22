using Cloyd.Chat;

namespace Cloyd.Codex.Chat;

/// <summary>A change in the conversation shown in the column, such as a turn ending with an interruption or a failure.</summary>
sealed record CodexChatNotice(string Id, string Line) : ShownInTheChat
{
   public override string TextShown => Line;
}
