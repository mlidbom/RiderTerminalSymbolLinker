using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;

namespace CloydSpike.Chat;

/// <summary>
/// The chat's input box: Avalonia's editor, AvaloniaEdit, as it comes, with Windows' text editing. Enter sends;
/// Shift+Enter is a new line.
/// </summary>
sealed class MessageBox : UserControl
{
   readonly TextEditor _editor = new()
   {
      ShowLineNumbers = false,
      WordWrap = true,
      FontFamily = new FontFamily("Segoe UI"),
      FontSize = 14,
      MinHeight = 60,
      MaxHeight = 240,
      Padding = new Avalonia.Thickness(6),
      VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
   };

   public MessageBox()
   {
      Content = new Border { BorderBrush = Brushes.DimGray, BorderThickness = new Avalonia.Thickness(1), CornerRadius = new Avalonia.CornerRadius(4), Child = _editor };
      _editor.TextArea.AddHandler(KeyDownEvent, SendOnEnter, Avalonia.Interactivity.RoutingStrategies.Tunnel);
   }

   /// <summary>Raised with what the box holds when Magnus sends it. The box is emptied once <see cref="Sent"/> says it was.</summary>
   public event Action<string>? Sending;

   public string Text { get => _editor.Text; set => _editor.Text = value; }

   /// <summary>Empties the box: what it held was sent.</summary>
   public void Sent() => _editor.Text = "";

   public void FocusTheBox() => _editor.TextArea.Focus();

   void SendOnEnter(object? sender, KeyEventArgs pressed)
   {
      if(pressed.Key != Key.Enter || pressed.KeyModifiers.HasFlag(KeyModifiers.Shift) || string.IsNullOrWhiteSpace(_editor.Text)) return;

      pressed.Handled = true;
      Sending?.Invoke(_editor.Text);
   }
}
