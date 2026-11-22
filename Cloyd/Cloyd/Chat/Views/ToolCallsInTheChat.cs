using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Cloyd.Chat.ToolCalls;
using Cloyd.Theming;
using static Cloyd.Chat.Views.LinesOfTheChat;

namespace Cloyd.Chat.Views;

/// <summary>
/// How the chat draws tool calls: a run of them as one grey line that counts them, which opens to a box with a row for
/// each; a call by itself as its row alone. A row opens to what the call was given and gave back.
/// </summary>
static class ToolCallsInTheChat
{
   /// <summary>The view of <paramref name="run"/>: it and its calls opened as Magnus has <paramref name="opened"/> them, which is told as he opens and closes them.</summary>
   public static Control Of(ShownInTheChat.RunOfToolCalls run, OpenedInTheChat opened) =>
      run.IsOneCall
         ? RowOf(run.Calls[0], opened)
         : Opening([Coloured(run.Counted, ColoursOfTheChat.Grey), .. LinesAddedBy(run.LinesAdded)], () => BoxOfTheRowsOf(run, opened), opened.Runs.IsOpen(run.Id), isOpen => opened.Runs.SetOpen(run.Id, isOpen));

   static Control BoxOfTheRowsOf(ShownInTheChat.RunOfToolCalls run, OpenedInTheChat opened)
   {
      var rows = new StackPanel();
      foreach(var call in run.Calls)
      {
         if(rows.Children.Count > 0) rows.Children.Add(ColoursOfTheChat.Border.AsTheBackgroundOf(new Border { Height = 1, Margin = new Thickness(-12, 6) }));
         rows.Children.Add(RowOf(call, opened));
      }

      return ColoursOfTheChat.Border.AsTheBorderOf(new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8), Child = rows });
   }

   /// <summary>The row of <paramref name="call"/>: its verb grey, what it acted on, the lines it added, how it failed, and a chevron.</summary>
   static Control RowOf(ToolCallInTheChat call, OpenedInTheChat opened) =>
      Opening([Coloured(call.Verb + " ", ColoursOfTheChat.Grey), Coloured(call.Target, ColoursOfTheChat.Text), .. LinesAddedBy(call.Outcome == OutcomeOfAToolCall.Done ? call.LinesAdded : 0), .. HowItEnded(call)],
              () => WhatWasGivenAndGivenBack(call), opened.Calls.IsOpen(call.Id), isOpen => opened.Calls.SetOpen(call.Id, isOpen));

   static Inline[] LinesAddedBy(int lines) => lines > 0 ? [Coloured($"  +{lines}", ColoursOfTheChat.Added)] : [];

   static Inline[] HowItEnded(ToolCallInTheChat call) => call.Outcome switch
   {
      OutcomeOfAToolCall.Failed => [Coloured("  Failed", ColoursOfTheChat.Failed)],
      OutcomeOfAToolCall.Stopped => [Coloured("  Stopped", ColoursOfTheChat.Grey)],
      _ => []
   };

   /// <summary>
   /// What an opened row shows: a command behind a <c>$ </c> in a bordered box, then its output; a change to a file, line
   /// by line; else what the tool gave back.
   /// </summary>
   static Control WhatWasGivenAndGivenBack(ToolCallInTheChat call)
   {
      var shown = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 0, 4) };
      if(call.Command is {} command)
         shown.Children.Add(ColoursOfTheChat.Border.AsTheBorderOf(new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 6), Child = InMonospace("$ " + command) }));
      if(call.Change is {} change && call.Outcome != OutcomeOfAToolCall.Failed)
         shown.Children.Add(LinesOf(change));
      else if(call.Result is {} result && result.Text.Length > 0)
         shown.Children.Add(InMonospace(result.Text, result.IsError ? ColoursOfTheChat.Failed : ColoursOfTheChat.Grey));
      return shown;
   }

   static Control LinesOf(ChangeToAFile change)
   {
      var lines = new StackPanel();
      foreach(var line in change.Lines)
      {
         var (sign, ground) = line.Kind switch
         {
            WhatHappenedToTheLine.Added => ("+ ", ColoursOfTheChat.GroundOfAnAddedLine),
            WhatHappenedToTheLine.Removed => ("- ", ColoursOfTheChat.GroundOfARemovedLine),
            _ => ("  ", (ColourOfTheTheme?)null)
         };
         var shown = new Border { Child = InMonospace(sign + line.Text) };
         ground?.AsTheBackgroundOf(shown);
         lines.Children.Add(shown);
      }

      return lines;
   }
}
