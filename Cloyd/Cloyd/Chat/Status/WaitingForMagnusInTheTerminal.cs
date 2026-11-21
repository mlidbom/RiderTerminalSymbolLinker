using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Cloyd.Hub.RunningSessions;

namespace Cloyd.Chat.Status;

/// <summary>
/// The box between the chat's conversation and its input box while Claude waits for Magnus in the terminal: a dialog of
/// Claude Code's own, such as a permission prompt, which only the terminal shows and answers. In Claude's orange, with a
/// button that shows the terminal, as Ctrl+H does. The tab doesn't switch by itself.
/// </summary>
/// <remarks>
/// Magnus, 2026-11-21, of the status line telling it alone: "Make it much more prominent than that. I could miss that for
/// hours."
/// </remarks>
sealed class WaitingForMagnusInTheTerminal : UserControl
{
   /// <summary>What the box says first, large.</summary>
   public const string Headline = "Claude waits for you in the terminal";

   /// <summary>What the button that shows the terminal says.</summary>
   public const string TextOfTheButton = "Show the terminal  (Ctrl+H)";

   static readonly IBrush ClaudesOrange = new SolidColorBrush(Color.Parse("#D97757"));

   readonly TextBlock _whatFor = new() { Foreground = Brushes.White, FontSize = 13, TextWrapping = TextWrapping.Wrap };

   public WaitingForMagnusInTheTerminal()
   {
      var showTheTerminal = new Button
      {
         Content = TextOfTheButton,
         Background = Brushes.White,
         Foreground = Brushes.Black,
         FontWeight = FontWeight.SemiBold,
         Padding = new Thickness(14, 8),
         CornerRadius = new CornerRadius(8),
         VerticalAlignment = VerticalAlignment.Center,
         Margin = new Thickness(12, 0, 0, 0)
      };
      showTheTerminal.Click += (_, _) => AsksForTheTerminal?.Invoke();
      DockPanel.SetDock(showTheTerminal, Dock.Right);
      IsVisible = false;
      Content = new Border
      {
         Background = ClaudesOrange,
         CornerRadius = new CornerRadius(12),
         Padding = new Thickness(18, 14),
         Margin = new Thickness(16, 8, 16, 4),
         Child = new DockPanel
         {
            Children =
            {
               showTheTerminal,
               new StackPanel
               {
                  Spacing = 2,
                  VerticalAlignment = VerticalAlignment.Center,
                  Children = { new TextBlock { Text = "⚠  " + Headline, Foreground = Brushes.White, FontSize = 17, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap }, _whatFor }
               }
            }
         }
      };
   }

   /// <summary>Raised when Magnus clicks the button that shows the terminal.</summary>
   public event Action? AsksForTheTerminal;

   /// <summary>What it says Claude waits for, such as <c>Permission prompt: answer it in the terminal.</c>: null while it doesn't show.</summary>
   public string? WhatFor => IsVisible ? _whatFor.Text : null;

   /// <summary>Shows the box while <paramref name="claudeCode"/>, the Claude Code that runs in the tab, waits for Magnus in a dialog of its own: null while none runs.</summary>
   public void Show(RunningClaudeCode? claudeCode)
   {
      IsVisible = claudeCode?.Status.IsWaiting == true;
      _whatFor.Text = claudeCode?.Status.WaitingFor is {Length: > 0} dialog
                         ? $"{char.ToUpperInvariant(dialog[0])}{dialog[1..]}: answer it in the terminal."
                         : "Answer it in the terminal.";
   }
}
