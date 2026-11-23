using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit;
using Cloyd.Chat.Views;

namespace Cloyd.Chat.Writing;

/// <summary>Shows the focused editor's Markdown while Ctrl+M is held. The original editor
/// keeps its document, selection, undo stack and keyboard focus under the preview.</summary>
sealed class DraftPreview : Grid
{
   readonly TextEditor _editor;
   readonly ScrollViewer _preview = new() { IsVisible = false, Focusable = false, Padding = new Thickness(12, 10, 4, 10) };
   Window? _window;
   bool _holdingM;

   public DraftPreview(TextEditor editor)
   {
      _editor = editor;
      Children.Add(editor);
      Children.Add(_preview);
      AddHandler(KeyDownEvent, Pressed, RoutingStrategies.Tunnel);
      AddHandler(KeyUpEvent, Released, RoutingStrategies.Tunnel, handledEventsToo: true);
      AddHandler(TextInputEvent, (_, input) =>
      {
         if(_holdingM && input.Text is "m" or "M") input.Handled = true;
      }, RoutingStrategies.Tunnel);
      editor.LostFocus += (_, _) => Reset();
   }

   protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
   {
      base.OnAttachedToVisualTree(e);
      _window = TopLevel.GetTopLevel(this) as Window;
      if(_window is not null) _window.Deactivated += Deactivated;
   }

   protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
   {
      if(_window is not null) _window.Deactivated -= Deactivated;
      _window = null;
      Reset();
      base.OnDetachedFromVisualTree(e);
   }

   void Pressed(object? sender, KeyEventArgs key)
   {
      if(key.Key == Key.M && _holdingM) { key.Handled = true; return; }
      if(key is not { Key: Key.M, KeyModifiers: KeyModifiers.Control } || !_editor.IsKeyboardFocusWithin) return;
      _holdingM = true;
      key.Handled = true;
      _preview.Content = ChatMarkdown.Render(_editor.Document.Text);
      _preview.IsVisible = true;
      _editor.Opacity = 0;
      _editor.IsReadOnly = true;
   }

   void Released(object? sender, KeyEventArgs key)
   {
      if(key.Key == Key.M) Reset();
      else if(key.Key is Key.LeftCtrl or Key.RightCtrl && !key.KeyModifiers.HasFlag(KeyModifiers.Control)) Hide();
   }

   void Deactivated(object? sender, EventArgs e) => Reset();

   public void Reset()
   {
      _holdingM = false;
      Hide();
   }

   public bool IsPreviewing => _preview.IsVisible;

   void Hide()
   {
      if(!IsPreviewing) return;
      _preview.IsVisible = false;
      _preview.Content = null;
      _editor.Opacity = 1;
      _editor.IsReadOnly = false;
   }
}
