using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Cloyd.ProjectsDashboard;

/// <summary>The decisions Magnus makes before removing a checkout. Escape and closing
/// the window both cancel, including when a session disposition is still undecided.</summary>
sealed class WorktreeRemovalDialog : Window
{
   protected override Type StyleKeyOverride => typeof(Window);
   public enum Choice { Cancel, Continue, Archive, Delete }

   WorktreeRemovalDialog(string message, params (string Label, Choice Choice)[] choices)
   {
      Title = "Remove worktree";
      WindowStartupLocation = WindowStartupLocation.CenterOwner;
      SizeToContent = SizeToContent.Height;
      Width = 470;
      CanResize = false;
      ShowInTaskbar = false;
      var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
      foreach(var (label, choice) in choices)
      {
         var button = new Button { Content = label };
         button.Click += (_, _) => Close(choice);
         buttons.Children.Add(button);
      }
      Content = new StackPanel { Margin = new Thickness(20), Spacing = 18, Children =
      {
         new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, buttons
      } };
      KeyDown += (_, key) => { if(key.Key == Key.Escape) { key.Handled = true; Close(Choice.Cancel); } };
   }

   public static Task<Choice> Warn(Window owner, string message) =>
      new WorktreeRemovalDialog(message, ("Cancel", Choice.Cancel), ("OK", Choice.Continue)).ShowDialog<Choice>(owner);

   public static Task<Choice> AskAboutSessions(Window owner, string message) =>
      new WorktreeRemovalDialog(message, ("Cancel", Choice.Cancel), ("Archive", Choice.Archive), ("Delete", Choice.Delete)).ShowDialog<Choice>(owner);
}
