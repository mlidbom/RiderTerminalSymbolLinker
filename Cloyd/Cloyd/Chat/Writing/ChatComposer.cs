using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using Cloyd.Pasting;

namespace Cloyd.Chat.Writing;

/// <summary>The multiline editor shared by the session chats. It keeps typed text and pasted images in a
/// <see cref="DraftOfAMessage"/> until the session's chat sends them.</summary>
abstract class ChatComposer : UserControl
{
   /// <summary>The tip of the button that sends: the key that does too.</summary>
   const string TipOfTheSendButton = "Send: Ctrl+Enter";

   /// <summary>The height of an image's thumbnail over the box, in logical pixels.</summary>
   const int HeightOfAThumbnail = 64;

   static readonly IBrush ClaudesOrange = new SolidColorBrush(Color.Parse("#D97757"));

   readonly PastedImages _pastedImages;
   readonly TextEditor _editor;
   readonly WrapPanel _thumbnails = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 8, 8, 0), IsVisible = false };
   readonly Button _send = RoundButton("↑", TipOfTheSendButton);
   readonly Button _stop;
   readonly TextBlock _notice = new() { IsVisible = false, Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 0, 4, 4) };

   /// <summary>The images whose thumbnails show, as the draft held them when they were last drawn.</summary>
   IReadOnlyList<PastedStretch.OfAnImage> _imagesShown = [];

   protected ChatComposer(PastedImages pastedImages, string watermark, string stopTip)
   {
      _pastedImages = pastedImages;
      _stop = RoundButton("■", stopTip);
      _editor = new TextEditor
      {
         Document = Draft.Document,
         Options = { AllowScrollBelowDocument = false },
         WordWrap = true,
         ShowLineNumbers = false,
         Watermark = watermark,
         HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
         VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
         Background = Brushes.Transparent,
         FontSize = 14,
         Padding = new Thickness(12, 10, 4, 10),
         MinHeight = 40
      };
      Draft.Changed += ShowTheThumbnailsOfTheImagesHeld;
      _send.Background = ClaudesOrange;
      _send.Foreground = Brushes.White;
      _send.Click += (_, _) => Send();
      _stop.IsVisible = false;
      _stop.Click += (_, _) => _ = Stop();
      AddHandler(KeyDownEvent, TakeItsKeys, RoutingStrategies.Tunnel);
      Content = TheLayout();
   }

   /// <summary>What Magnus writes, until he sends it.</summary>
   public DraftOfAMessage Draft { get; private set; } = new();

   /// <summary>Shows the draft of the session now in the terminal, including the images pasted into it.</summary>
   protected void Edit(DraftOfAMessage draft)
   {
      if(Draft == draft) return;
      Draft.Changed -= ShowTheThumbnailsOfTheImagesHeld;
      Draft = draft;
      _editor.Document = draft.Document;
      _editor.CaretOffset = draft.Document.TextLength;
      Draft.Changed += ShowTheThumbnailsOfTheImagesHeld;
      ShowTheThumbnailsOfTheImagesHeld();
   }

   public bool ShowsTheStopButton
   {
      get => _stop.IsVisible;
      protected set => _stop.IsVisible = value;
   }

   /// <summary>Whether the Send button can submit now. The editor remains available while the connection is starting.</summary>
   public bool CanSend
   {
      get => _send.IsEnabled;
      protected set => _send.IsEnabled = value;
   }

   /// <summary>What the box tells over it, as why Claude Code didn't get a message: null while it tells nothing.</summary>
   public string? Notice => _notice.IsVisible ? _notice.Text : null;

   /// <summary>The placeholders of the images whose thumbnails show over the box, in order, such as <c>[Image #1]</c>.</summary>
   public IReadOnlyList<string> Thumbnails => [.. _imagesShown.Select(it => it.Placeholder)];

   /// <summary>The highest the box grows, in logical pixels, as Magnus writes more lines: past it, it scrolls.</summary>
   public double MostHeight
   {
      set => _editor.MaxHeight = value;
   }

   /// <summary>Brings back what the box saved, <paramref name="saved"/>, as its window opens.</summary>
   public void BringBack(IReadOnlyList<PartOfAMessage> saved) => Draft.PutBackAhead(saved);

   /// <summary>Gives the box the keyboard, for Magnus to write.</summary>
   public void TakeTheKeyboard() => _editor.TextArea.Focus();

   protected abstract void Send();
   protected abstract Task Stop();

   /// <summary>
   /// Takes the box's own keys before its editor does: Ctrl+Enter sends; Ctrl+V and Shift+Insert paste, marked as
   /// pasted.
   /// </summary>
   void TakeItsKeys(object? sender, KeyEventArgs key)
   {
      switch(key)
      {
         case { Key: Key.Enter, KeyModifiers: KeyModifiers.Control }:
            key.Handled = true;
            Send();
            break;
         case { Key: Key.V, KeyModifiers: KeyModifiers.Control } or { Key: Key.Insert, KeyModifiers: KeyModifiers.Shift }:
            key.Handled = true;
            _ = PasteWhatTheClipboardHolds();
            break;
      }
   }

   /// <summary>Pastes the clipboard's text, else its image, in place of the selection: nothing when it holds neither. A failure is told over the box.</summary>
   async Task PasteWhatTheClipboardHolds()
   {
      try
      {
         var pasted = await WhatCtrlVPastes.From(TopLevel.GetTopLevel(this)!.Clipboard!);
         var selection = new TextSegment { StartOffset = _editor.SelectionStart, Length = _editor.SelectionLength };
         var before = Draft.Document.TextLength;
         switch(pasted)
         {
            case WhatCtrlVPastes.Text text:
               Draft.Paste(text.Value.ReplaceLineEndings("\n"), selection);
               break;
            case WhatCtrlVPastes.AnImage image:
               Draft.Paste(_pastedImages.Save(image.Image), selection);
               break;
            default:
               return;
         }

         _editor.CaretOffset = selection.StartOffset + selection.Length + Draft.Document.TextLength - before;
         _editor.TextArea.ClearSelection();
      }
      catch(Exception failure)
      {
         Tell($"Cloyd couldn't paste: {failure.Message}");
      }
   }

   /// <summary>Shows a thumbnail of each image the draft holds, over the box, as it holds them: once they change.</summary>
   void ShowTheThumbnailsOfTheImagesHeld()
   {
      var images = Draft.Images;
      if(images.SequenceEqual(_imagesShown)) return;
      _imagesShown = images;
      _thumbnails.Children.Clear();
      foreach(var image in images) _thumbnails.Children.Add(ThumbnailOf(image));
      _thumbnails.IsVisible = images.Count > 0;
   }

   /// <summary>A thumbnail of <paramref name="image"/>, with its placeholder under it: a click shows the image whole, in Windows' viewer.</summary>
   Control ThumbnailOf(PastedStretch.OfAnImage image)
   {
      using var file = image.File.OpenRead();
      var thumbnail = new Button
      {
         Padding = new Thickness(4),
         Margin = new Thickness(0, 0, 8, 0),
         Background = Brushes.Transparent,
         Content = new StackPanel
         {
            Spacing = 2,
            Children =
            {
               new Image { Source = Bitmap.DecodeToHeight(file, HeightOfAThumbnail), Height = HeightOfAThumbnail },
               new TextBlock { Text = image.Placeholder, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center }
            }
         }
      };
      ToolTip.SetTip(thumbnail, "Show the image");
      thumbnail.Click += (_, _) => _ = TopLevel.GetTopLevel(this)!.Launcher.LaunchFileInfoAsync(image.File);
      return thumbnail;
   }

   protected void Tell(string? notice)
   {
      _notice.Text = notice;
      _notice.IsVisible = notice is not null;
   }

   /// <summary>The box as the app's composer: rounded, a shade off the conversation, its buttons at its right; the notice over it.</summary>
   Control TheLayout()
   {
      var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(4, 4, 8, 8), Children = { _stop, _send } };
      DockPanel.SetDock(buttons, Dock.Right);
      DockPanel.SetDock(_thumbnails, Dock.Top);
      var box = new Border
      {
         CornerRadius = new CornerRadius(12),
         BorderThickness = new Thickness(1),
         Child = new DockPanel { Children = { _thumbnails, buttons, _editor } }
      };
      box.Bind(Border.BorderBrushProperty, box.GetResourceObservable("TextControlBorderBrush"));
      box.Bind(Border.BackgroundProperty, box.GetResourceObservable("TextControlBackground"));
      DockPanel.SetDock(_notice, Dock.Top);
      return new DockPanel { Margin = new Thickness(16, 4, 16, 6), Children = { _notice, box } };
   }

   static Button RoundButton(string content, string tip)
   {
      var button = new Button
      {
         Content = content,
         Width = 32,
         Height = 32,
         CornerRadius = new CornerRadius(16),
         Padding = new Thickness(0),
         HorizontalContentAlignment = HorizontalAlignment.Center,
         VerticalContentAlignment = VerticalAlignment.Center,
         FontSize = 15
      };
      ToolTip.SetTip(button, tip);
      return button;
   }
}
