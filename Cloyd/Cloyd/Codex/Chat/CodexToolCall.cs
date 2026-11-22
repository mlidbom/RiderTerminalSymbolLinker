using Avalonia.Controls;
using Cloyd.Theming;
using Cloyd.Chat.Views;
using static Cloyd.Chat.Views.LinesOfTheChat;

namespace Cloyd.Codex.Chat;

/// <summary>A tool call as it appears in a Codex turn. Its expanded row shows the command or tool input and
/// the output Codex retained. The same row is updated when the call ends.</summary>
sealed record CodexToolCall(string Id, string Label, CodexToolCallDetails Details)
{
   public string Heading => Details.Failed ? $"{Label} (failed)" : Label;

   public Control Draw(OpenedChatRows openedCalls) =>
      Opening([Coloured(Heading, Details.Failed ? ColoursOfTheChat.Failed : ColoursOfTheChat.Text)],
              DrawDetails, openedCalls.IsOpen(Id), open => openedCalls.SetOpen(Id, open));

   Control DrawDetails()
   {
      var panel = new StackPanel { Spacing = 6 };
      if(Details.Input is {Length: > 0} input) panel.Children.Add(InMonospace(input));
      if(Details.Output is {Length: > 0} output) panel.Children.Add(InMonospace(output, Details.Failed ? ColoursOfTheChat.Failed : ColoursOfTheChat.Grey));
      if(Details.Patch is {Length: > 0} patch)
         foreach(var line in patch.ReplaceLineEndings("\n").Split('\n')) panel.Children.Add(LineOfPatch(line));
      if(Details.Outcome is {Length: > 0} outcome) panel.Children.Add(Grey(outcome));
      return panel;
   }

   static Control LineOfPatch(string line)
   {
      var shown = new Border { Child = InMonospace(line) };
      var ground = line.StartsWith('+') ? ColoursOfTheChat.GroundOfAnAddedLine : line.StartsWith('-') ? ColoursOfTheChat.GroundOfARemovedLine : (ColourOfTheTheme?)null;
      return ground?.AsTheBackgroundOf(shown) ?? shown;
   }
}
