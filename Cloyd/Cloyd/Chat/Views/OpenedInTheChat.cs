namespace Cloyd.Chat.Views;

/// <summary>
/// What Magnus has opened in the chat: runs of tool calls, and the calls in them. What he opened stays open as the chat
/// draws it again, as when a call is added to the run, or when the hub's stream sends the conversation again.
/// </summary>
sealed class OpenedInTheChat
{
   public OpenedChatRows Runs { get; } = new();
   public OpenedChatRows Calls { get; } = new();
}
