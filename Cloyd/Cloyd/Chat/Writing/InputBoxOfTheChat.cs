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
using Cloyd.Hub;
using Cloyd.Hub.RunningSessions;
using Cloyd.Pasting;

namespace Cloyd.Chat.Writing;

/// <summary>
/// The chat's input box, where Magnus writes a message to the Claude Code that runs in the tab, as the Claude desktop
/// app's composer is, and sends it: through the hub, which types it into Claude Code's own box as he would.
/// </summary>
/// <remarks>
/// Its keys, as Magnus chose them (2026-11-21; <c>dev_docs/cloyd-as-a-dotnet-application.md</c>, "The input box,
/// Magnus's calls"):
/// <list type="bullet">
/// <item>Ctrl+Enter sends; Enter starts a new line, as in every editor.</item>
/// <item>Nothing interrupts Claude by accident. Escape does nothing to it, nor does sending: a message sent while Claude
/// works waits, and Claude Code takes it when it can. Only the Stop button, shown while Claude works, interrupts.</item>
/// <item>Ctrl+V pastes text as pasted, apart from what he types, and an image with no text on the clipboard as an image,
/// shown as a thumbnail over the box.</item>
/// </list>
/// </remarks>
sealed class InputBoxOfTheChat : UserControl
{
   /// <summary>The tip of the button that sends: the key that does too.</summary>
   public const string TipOfTheSendButton = "Send: Ctrl+Enter";

   /// <summary>The tip of the button that stops Claude.</summary>
   public const string TipOfTheStopButton = "Stop Claude";

   /// <summary>What the box says while it is empty.</summary>
   const string Watermark = "Reply to Claude…";

   /// <summary>The height of an image's thumbnail over the box, in logical pixels.</summary>
   const int HeightOfAThumbnail = 64;

   static readonly IBrush ClaudesOrange = new SolidColorBrush(Color.Parse("#D97757"));

   readonly TheHub _hub;
   readonly PastedImages _pastedImages;
   readonly SendingFromTheChat _sending;
   readonly TextEditor _editor;
   readonly WrapPanel _thumbnails = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 8, 8, 0), IsVisible = false };
   readonly Button _send = RoundButton("↑", TipOfTheSendButton);
   readonly Button _stop = RoundButton("■", TipOfTheStopButton);
   readonly TextBlock _notice = new() { IsVisible = false, Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 0, 4, 4) };

   /// <summary>The images whose thumbnails show, as the draft held them when they were last drawn.</summary>
   IReadOnlyList<PastedStretch.OfAnImage> _imagesShown = [];

   /// <summary>A box that sends through <paramref name="hub"/>, and saves the images pasted into it in <paramref name="pastedImages"/>.</summary>
   public InputBoxOfTheChat(TheHub hub, PastedImages pastedImages)
   {
      _hub = hub;
      _pastedImages = pastedImages;
      _sending = new SendingFromTheChat(hub);
      _sending.NotGiven += PutBackWhatClaudeCodeDidntGet;
      _sending.OnTheirWayChanged += () => MessagesOnTheirWayChanged?.Invoke();
      _editor = new TextEditor
      {
         Document = Draft.Document,
         WordWrap = true,
         ShowLineNumbers = false,
         Watermark = Watermark,
         HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
         VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
         Background = Brushes.Transparent,
         FontSize = 14,
         Padding = new Thickness(12, 10, 4, 10),
         MinHeight = 40
      };
      _editor.Options.AllowScrollBelowDocument = false;
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
   public DraftOfAMessage Draft { get; } = new();

   /// <summary>
   /// The Claude Code the box sends to: the one that runs in the tab's host, as the hub last told. Null while none runs, as
   /// before it starts, or after it exits to the shell.
   /// </summary>
   public RunningClaudeCode? ClaudeCode
   {
      get;
      set
      {
         field = value;
         _stop.IsVisible = value?.Status.IsBusy == true;
         _sending.ClaudeCodeRuns(value?.Process);
      }
   }

   /// <summary>Whether the Stop button shows: while Claude works.</summary>
   public bool ShowsTheStopButton => _stop.IsVisible;

   /// <summary>What the box tells over it, as why Claude Code didn't get a message: null while it tells nothing.</summary>
   public string? Notice => _notice.IsVisible ? _notice.Text : null;

   /// <summary>The placeholders of the images whose thumbnails show over the box, in order, such as <c>[Image #1]</c>.</summary>
   public IReadOnlyList<string> Thumbnails => [.. _imagesShown.Select(it => it.Placeholder)];

   /// <summary>Whether messages sent are still being given to Claude Code, or wait to be.</summary>
   public bool IsGivingMessages => _sending.IsGiving;

   /// <summary>The messages sent that the conversation doesn't have yet, in the order Magnus sent them: the chat shows them over the box (<see cref="MessagesOnTheirWayOfTheChat"/>).</summary>
   public IReadOnlyList<MessageOnItsWay> MessagesOnTheirWay => _sending.OnTheirWay;

   /// <summary>Raised on the UI thread when <see cref="MessagesOnTheirWay"/> has changed.</summary>
   public event Action? MessagesOnTheirWayChanged;

   /// <summary>The chat shows <paramref name="conversation"/> now: the messages on their way that it has are on their way no more.</summary>
   public void Seen(IReadOnlyList<ShownInTheChat> conversation) => _sending.Seen(conversation);

   /// <summary>The highest the box grows, in logical pixels, as Magnus writes more lines: past it, it scrolls.</summary>
   public double MostHeight
   {
      set => _editor.MaxHeight = value;
   }

   /// <summary>
   /// What the box saves with its window: the messages that waited to be given to Claude Code, a blank line apart, then
   /// what Magnus was writing. All of it comes back into the box with the window, as the draft, to be sent again by him.
   /// </summary>
   public IReadOnlyList<PartOfAMessage> AsSaved => [.. TheParts(_sending.Waiting), .. Draft.Parts];

   /// <summary>Brings back what the box saved, <paramref name="saved"/>, as its window opens.</summary>
   public void BringBack(IReadOnlyList<PartOfAMessage> saved) => Draft.PutBackAhead(saved);

   /// <summary>Raised when Magnus has sent a slash command, such as <c>/compact</c>, which shows what it does in Claude Code's own UI.</summary>
   public event Action? SentASlashCommand;

   /// <summary>Gives the box the keyboard, for Magnus to write.</summary>
   public void TakeTheKeyboard() => _editor.TextArea.Focus();

   /// <summary>
   /// Sends what the box holds, as Ctrl+Enter and the send button do: given to the Claude Code that runs in the tab, and
   /// the box empty for the next message at once. Nothing when it holds nothing; told over the box, and kept in it, while
   /// no Claude Code runs.
   /// </summary>
   public void Send()
   {
      if(Draft.IsEmpty) return;
      if(ClaudeCode is not {} claudeCode)
      {
         Tell("No Claude Code runs in this tab: start one in its terminal, and send again.");
         return;
      }

      _notice.IsVisible = false;
      var message = Draft.TakeTheMessage();
      _sending.Give(message, claudeCode.Process);
      if(message.IsASlashCommand) SentASlashCommand?.Invoke();
   }

   /// <summary>Interrupts Claude, as the Stop button asks: told over the box when the hub didn't, as when Claude was done already.</summary>
   async Task Stop()
   {
      if(ClaudeCode is not {} claudeCode) return;
      try
      {
         await _hub.Interrupt(claudeCode.Process);
      }
      catch(Exception failure)
      {
         Tell($"Claude wasn't stopped: {failure.Message}");
      }
   }

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

   /// <summary>
   /// Puts back into the box the messages Claude Code didn't get, <paramref name="notGiven"/>, ahead of what Magnus wrote
   /// since, and tells <paramref name="why"/> over it, for him to send them again once he has seen to it.
   /// </summary>
   void PutBackWhatClaudeCodeDidntGet(IReadOnlyList<MessageFromTheChat> notGiven, string why)
   {
      Draft.PutBackAhead(TheParts(notGiven));
      Tell($"Claude Code didn't get the message, which is back in the box: {why}");
   }

   /// <summary>The parts of <paramref name="messages"/>, in order, a blank line between one and the next.</summary>
   static IReadOnlyList<PartOfAMessage> TheParts(IReadOnlyList<MessageFromTheChat> messages) =>
      [.. messages.SelectMany((message, at) => at == 0 ? message.Parts : [new PartOfAMessage.TypedText("\n\n"), .. message.Parts])];

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

   void Tell(string notice)
   {
      _notice.Text = notice;
      _notice.IsVisible = true;
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
