using Cloyd.Chat;

namespace Cloyd.Codex.Chat;

/// <summary>A piece of Codex's commentary or final answer, in its original position among the other items.</summary>
sealed record CodexReply(string Id, string Markdown) : ShownInTheChat
{
   public override string TextShown => Markdown;
}
