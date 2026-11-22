using Avalonia.Controls;
using Cloyd.Chat.Views;
using static Cloyd.Chat.Views.LinesOfTheChat;

namespace Cloyd.Codex.Chat;

/// <summary>A tool call as it appears in a Codex turn. Its expanded row shows the command or tool input and
/// the output Codex retained. The same row is updated when the call ends.</summary>
sealed record CodexToolCall(string Id, string Label, string? Input, string? Output, bool Failed)
{
   public Control Draw(OpenedChatRows openedCalls) =>
      Opening([Coloured(Label, Failed ? ColoursOfTheChat.Failed : ColoursOfTheChat.Text)],
              Details, openedCalls.IsOpen(Id), open => openedCalls.SetOpen(Id, open));

   Control Details()
   {
      var panel = new StackPanel { Spacing = 6 };
      if(Input is {Length: > 0}) panel.Children.Add(InMonospace(Input));
      if(Output is {Length: > 0}) panel.Children.Add(InMonospace(Output, Failed ? ColoursOfTheChat.Failed : ColoursOfTheChat.Grey));
      return panel;
   }
}
