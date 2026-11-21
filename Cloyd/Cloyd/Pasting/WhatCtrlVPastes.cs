using Avalonia.Input.Platform;

namespace Cloyd.Pasting;

/// <summary>
/// What Ctrl+V pastes from the clipboard, into a terminal or the chat's input box: its text, when it holds any, else
/// its image, as a screenshot puts one there with no text.
/// </summary>
abstract partial record WhatCtrlVPastes
{
   /// <summary>What Ctrl+V pastes from <paramref name="clipboard"/> now: null when it holds neither text nor an image.</summary>
   public static async Task<WhatCtrlVPastes?> From(IClipboard clipboard) =>
      await clipboard.TryGetTextAsync() is { Length: > 0 } text ? new Text(text)
      : await clipboard.TryGetBitmapAsync() is {} image ? new AnImage(image)
      : null;
}
