using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Cloyd.Chat.Views;

namespace Cloyd.Codex.Chat.Status;

/// <summary>The fixed rows above the Codex composer. The latest active call and its input sit above
/// the turn's <see cref="CodexProgressDot"/>, activity, elapsed time and command count.</summary>
sealed class ProgressOfACodexChat : UserControl
{
   readonly TextBlock _call = new() { FontSize = 13, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
   readonly TextBlock _input = new() { FontFamily = LinesOfTheChat.Monospaced, FontSize = 12, MaxLines = 3, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(4, 0, 0, 2) };
   readonly CodexProgressDot _dot = new();
   readonly Run _activity = new() { FontWeight = FontWeight.SemiBold };
   readonly Run _counts = new();
   readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
   CodexTurnProgress? _progress;

   public ProgressOfACodexChat()
   {
      IsVisible = false;
      ColoursOfTheChat.Text.AsTheForegroundOf(_call);
      ColoursOfTheChat.Grey.AsTheForegroundOf(_input);
      ColoursOfTheChat.CodexActivity.AsTheForegroundOf(_activity);
      var working = ColoursOfTheChat.Grey.AsTheForegroundOf(new TextBlock { FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
      working.Inlines!.Add(_activity);
      working.Inlines.Add(_counts);
      DockPanel.SetDock(_dot, Dock.Left);
      Content = new StackPanel { Margin = new Thickness(28, 2, 28, 2), Children = { _call, _input, new DockPanel { Children = { _dot, working } } } };
      _clock.Tick += (_, _) => UpdateTimes();
   }

   public string? WorkingLine => IsVisible ? _activity.Text + _counts.Text : null;
   public string? CurrentCall => _call.IsVisible ? _call.Text : null;
   public string? CurrentInput => _input.IsVisible ? _input.Text : null;

   public void Show(CodexTurnProgress? progress)
   {
      _progress = progress;
      IsVisible = progress is not null;
      _dot.IsPulsing = IsVisible;
      _clock.IsEnabled = progress?.StartedAt is not null || progress?.Calls.LastOrDefault()?.StartedAt is not null;
      _activity.Text = progress?.Activity;
      _input.Text = progress?.Calls.LastOrDefault()?.PreviewOfInput;
      _input.IsVisible = _input.Text is not null;
      UpdateTimes();
   }

   void UpdateTimes()
   {
      var now = DateTimeOffset.UtcNow;
      _counts.Text = _progress?.CountsAt(now);
      _call.Text = _progress?.Calls.LastOrDefault()?.HeadingAt(now, _progress.Calls.Count - 1);
      _call.IsVisible = _call.Text is not null;
   }
}
