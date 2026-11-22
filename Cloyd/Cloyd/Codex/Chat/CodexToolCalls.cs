using Avalonia;
using Avalonia.Controls;
using Cloyd.Chat;
using Cloyd.Chat.Views;
using static Cloyd.Chat.Views.LinesOfTheChat;

namespace Cloyd.Codex.Chat;

/// <summary>Consecutive tool calls within one Codex turn. A single call opens directly, while a group
/// opens to its individual calls. Expansion follows the item IDs across stream updates.</summary>
sealed record CodexToolCalls(IReadOnlyList<CodexToolCall> Calls) : ShownInTheChat
{
   public override string TextShown => Calls.Count == 1 ? Calls[0].Heading : $"{Calls.Count} tool calls" + Failures;

   string Failures => Calls.Count(it => it.Details.Failed) is var count and > 0 ? $", {count} failed" : "";

   public Control Draw(OpenedInTheChat opened)
   {
      var first = Calls[0];
      if(Calls.Count == 1) return first.Draw(opened.Calls);
      return Opening([Coloured(TextShown, ColoursOfTheChat.Grey)], Details, opened.Runs.IsOpen(first.Id),
                     open => opened.Runs.SetOpen(first.Id, open));

      Control Details()
      {
         var rows = new StackPanel { Spacing = 6 };
         foreach(var call in Calls) rows.Children.Add(call.Draw(opened.Calls));
         return ColoursOfTheChat.Border.AsTheBorderOf(new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8), Child = rows });
      }
   }

   public bool Equals(CodexToolCalls? other) => other is not null && Calls.SequenceEqual(other.Calls);
   public override int GetHashCode() => HashCode.Combine(Calls[0].Id, Calls.Count);
}
