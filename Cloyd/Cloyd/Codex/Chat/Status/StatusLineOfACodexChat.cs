using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;
using Cloyd.Chat.Status;
using Cloyd.Chat.Views;

namespace Cloyd.Codex.Chat.Status;

/// <summary>The session's configured Codex status line below the composer. Text wraps at the chat width
/// and uses Cloyd's light or dark equivalents of terminal colours.</summary>
sealed class StatusLineOfACodexChat : UserControl
{
   readonly TextBlock _items = GreyLine();
   readonly TextBlock _permissions = GreyLine();
   CodexChatFooter? _shown;

   public StatusLineOfACodexChat()
   {
      Resources.MergedDictionaries.Add(ColoursOfTheTerminal.AsResources());
      IsVisible = false;
      Content = new StackPanel { Margin = new Thickness(28, 0, 28, 4), Spacing = 2, Children = { _items, _permissions } };
   }

   public string TextShown => LinesOfTheChat.TextIn(_items);
   public string? PermissionsShown => _permissions.Text;

   public void Show(CodexChatFooter? footer)
   {
      if(ReferenceEquals(footer, _shown)) return;
      _shown = footer;
      _items.Inlines!.Clear();
      _permissions.Text = null;
      if(footer is not null)
      {
         var line = new CodexStatusLineItems(footer);
         foreach(var (text, colour) in line.InOrder())
         {
            if(_items.Inlines.Count > 0) _items.Inlines.Add(new Run(" · "));
            var run = new Run(text);
            if(footer.UseColors) ColoursOfTheTerminal.Named(colour)?.AsTheForegroundOf(run);
            _items.Inlines.Add(run);
         }
         _permissions.Text = line.PermissionsLine is {Length: > 0} permissions ? permissions : null;
      }
      _items.IsVisible = _items.Inlines.Count > 0;
      _permissions.IsVisible = _permissions.Text is not null;
      IsVisible = _items.IsVisible || _permissions.IsVisible;
   }

   static TextBlock GreyLine() => ColoursOfTheChat.Grey.AsTheForegroundOf(new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap });
}
