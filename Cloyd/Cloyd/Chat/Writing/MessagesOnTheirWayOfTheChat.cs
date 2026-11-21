using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Cloyd.Chat.Views;

namespace Cloyd.Chat.Writing;

/// <summary>
/// The messages Magnus sent that the conversation doesn't have yet (<see cref="MessageOnItsWay"/>), each as the
/// conversation's last prompt would show, dimmed, with its <see cref="MessageOnItsWay.Stage"/> under it, such as
/// <c>Queued</c>, as Claude Code's own UI shows a queued message. Between the tool call under way, which they wait behind,
/// and the turn's line (Magnus, 2026-11-21: "place queued messages above the turn information with the spinning icon, but
/// below what the session is doing currently which is the reason the message is queued"). No room while there are none.
/// </summary>
sealed class MessagesOnTheirWayOfTheChat : UserControl
{
   /// <summary>How many lines of a message show: what is longer is cut, with an ellipsis.</summary>
   const int LinesShownOfAMessage = 3;

   readonly StackPanel _messages = new() { Spacing = 6, Margin = new Thickness(16, 2, 16, 6) };

   public MessagesOnTheirWayOfTheChat()
   {
      IsVisible = false;
      Content = _messages;
   }

   /// <summary>Shows <paramref name="onTheirWay"/>, in the order Magnus sent them, in place of those it shows.</summary>
   public void Show(IReadOnlyList<MessageOnItsWay> onTheirWay)
   {
      _messages.Children.Clear();
      foreach(var message in onTheirWay) _messages.Children.Add(ViewOf(message));
      IsVisible = onTheirWay.Count > 0;
   }

   static Control ViewOf(MessageOnItsWay message)
   {
      var bubble = LinesOfTheChat.Bubble(LinesShownOfAMessage, new Run(message.Message.AsShownInTheChat));
      var stage = ColoursOfTheChat.Grey.AsTheForegroundOf(new TextBlock { Text = message.Stage, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 2, 6, 0) });
      return new StackPanel { Opacity = 0.6, Children = { bubble, stage } };
   }
}
