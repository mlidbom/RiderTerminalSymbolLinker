namespace Cloyd.Chat.Views;

/// <summary>Rows Magnus has expanded, by their identity in the conversation, so expanding survives a redraw.</summary>
sealed class OpenedChatRows
{
   readonly HashSet<string> _opened = [];

   public bool IsOpen(string id) => _opened.Contains(id);

   public void SetOpen(string id, bool open)
   {
      if(open) _opened.Add(id);
      else _opened.Remove(id);
   }
}
