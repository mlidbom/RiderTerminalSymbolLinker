using Cloyd.Chat.ToolCalls;

namespace Cloyd.Chat.Views;

/// <summary>
/// What Magnus has opened in the chat: runs of tool calls, and the calls in them. What he opened stays open as the chat
/// draws it again, as when a call is added to the run, or when the hub's stream sends the conversation again.
/// </summary>
sealed class OpenedInTheChat
{
   /// <summary>The runs opened, by their <see cref="ShownInTheChat.RunOfToolCalls.Id"/>, which is their first call's.</summary>
   readonly HashSet<string> _runs = [];

   /// <summary>The calls opened, by their <see cref="ToolCallInTheChat.Id"/>.</summary>
   readonly HashSet<string> _calls = [];

   public bool IsOpen(ShownInTheChat.RunOfToolCalls run) => _runs.Contains(run.Id);

   public bool IsOpen(ToolCallInTheChat call) => _calls.Contains(call.Id);

   /// <summary>Notes that Magnus opened <paramref name="run"/>, when <paramref name="isOpen"/>, or closed it.</summary>
   public void Toggled(ShownInTheChat.RunOfToolCalls run, bool isOpen) => Toggled(_runs, run.Id, isOpen);

   /// <summary>Notes that Magnus opened <paramref name="call"/>, when <paramref name="isOpen"/>, or closed it.</summary>
   public void Toggled(ToolCallInTheChat call, bool isOpen) => Toggled(_calls, call.Id, isOpen);

   static void Toggled(HashSet<string> opened, string id, bool isOpen)
   {
      if(isOpen) opened.Add(id);
      else opened.Remove(id);
   }
}
