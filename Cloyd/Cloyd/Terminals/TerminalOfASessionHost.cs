using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Pasting;
using Cloyd.SessionHosts;
using Cloyd.Sessions;
using Iciclecreek.Terminal;
using Porta.Pty;

namespace Cloyd.Terminals;

/// <summary>
/// The terminal of a session host: Iciclecreek's terminal control, on XTerm.NET, drawing what the host's pseudo console
/// draws and typing into it, as a terminal does for a pseudo console of its own. It speaks the console's win32 input
/// mode, which the host's console asks for, so that each key reaches the agent CLI in the host exactly, with its modifiers,
/// Escape included.
/// </summary>
sealed class TerminalOfASessionHost : TerminalControl
{
   /// <summary>How many lines scrolled off the top it keeps.</summary>
   const int LinesItKeeps = 5000;

   /// <summary>
   /// What Shift+Enter types in Claude Code: a line feed, which it takes from a pseudo console as Ctrl+J, its key for a
   /// new line in its input box, as the IDE's terminal typing one found (claude-sessions.md, "The chat view: what the
   /// spikes found").
   /// </summary>
   const string ANewLineInClaudeCodesInputBox = "\n";

   /// <summary>What a terminal sends ahead of a bracketed paste, once the program in it has asked for those.</summary>
   const string StartOfABracketedPaste = "\u001b[200~";

   /// <summary>What a terminal sends after a bracketed paste.</summary>
   const string EndOfABracketedPaste = "\u001b[201~";

   readonly PastedImages _pastedImages;
   readonly Func<AgentCli> _agentCliInTheHost;
   bool _attached;

   /// <summary>
   /// The key now held whose press the terminal didn't get, as Cloyd typed something else for it: its release is then no
   /// key of the terminal's either. Null while none is.
   /// </summary>
   Key? _heldKeyTakenFromTheTerminal;

   /// <summary>
   /// Attaches to the host of <paramref name="attachment"/> once laid out. The images pasted into it are saved in
   /// <paramref name="pastedImages"/>. <paramref name="agentCliInTheHost"/> says which agent CLI the host runs now,
   /// which can change while the terminal shows it.
   /// </summary>
   public TerminalOfASessionHost(AttachmentToASessionHost attachment, PastedImages pastedImages, Func<AgentCli> agentCliInTheHost)
   {
      _pastedImages = pastedImages;
      _agentCliInTheHost = agentCliInTheHost;
      FontFamily = new FontFamily("Cascadia Mono");
      FontSize = 14;
      BufferSize = LinesItKeeps;
      TemplateApplied += (_, _) =>
      {
         if(_attached) return;
         _attached = true;
         AttachConnection(new SessionHostAsAPseudoConsole(attachment));
      };
      AddHandler(KeyDownEvent, TypeANewLineOnAShiftEnterThatWouldSend, RoutingStrategies.Tunnel);
      AddHandler(KeyDownEvent, PasteOnCtrlV, RoutingStrategies.Tunnel);
      AddHandler(KeyUpEvent, LetGoOfAKeyTakenFromTheTerminal, RoutingStrategies.Tunnel);
      UrlClicked += OpenAWebPagesUrlInTheBrowser;
   }

   /// <summary>Raised on the UI thread when what Magnus pasted with Ctrl+V couldn't be pasted, with why.</summary>
   public event Action<Exception>? PasteFailed;

   protected override Type StyleKeyOverride => typeof(TerminalControl);

   /// <summary>What it shows now, line by line, as text: nothing until it is first laid out.</summary>
   public string TextShown => HasBeenLaidOut ? Terminal.Buffer.PrintViewport() : "";

   /// <summary>Its size in character cells, which the host's pseudo console is resized to: null until it is first laid out.</summary>
   public ConsoleSize? Size => HasBeenLaidOut ? new ConsoleSize((short)Terminal.Buffer.Cols, (short)Terminal.Buffer.Rows) : null;

   /// <summary>
   /// Whether it has been laid out in a window, whose template gives it the view that holds its
   /// <see cref="TerminalControl.Terminal"/>: a tab not yet shown has none, and reading it then throws.
   /// </summary>
   bool HasBeenLaidOut => _attached;

   /// <summary>
   /// Has Shift+Enter type a new line, as it does in every editor and did in the IDE's terminal, in an agent CLI that
   /// <see cref="AgentCli.IgnoresTheShiftOnEnter"/>, as Claude Code does: there it would send the message. In Codex,
   /// which starts a new line on Shift+Enter itself, the key reaches the host as pressed.
   /// </summary>
   void TypeANewLineOnAShiftEnterThatWouldSend(object? sender, KeyEventArgs key)
   {
      if(!IsAShiftEnterThatWouldSend()) return;
      TakeFromTheTerminal(key);
      _ = SendInputAsync(ANewLineInClaudeCodesInputBox);

      bool IsAShiftEnterThatWouldSend() => key is { Key: Key.Enter, KeyModifiers: KeyModifiers.Shift } && _agentCliInTheHost().IgnoresTheShiftOnEnter;
   }

   /// <summary>
   /// Has Ctrl+V paste, as Ctrl+Shift+V does, and as it does in every other Windows program: in Claude Code on Windows it
   /// does nothing, as its own key for pasting an image is Alt+V. Text on the clipboard is pasted as text; an image, when
   /// the clipboard holds no text, is saved by <see cref="PastedImages"/> and its file's path pasted alone, which Claude
   /// Code takes for the image.
   /// </summary>
   void PasteOnCtrlV(object? sender, KeyEventArgs key)
   {
      if(key is not { Key: Key.V, KeyModifiers: KeyModifiers.Control }) return;
      TakeFromTheTerminal(key);
      _ = PasteWhatTheClipboardHolds();
   }

   /// <summary>Pastes the clipboard's text, else its image: nothing when it holds neither. A failure is told by <see cref="PasteFailed"/>.</summary>
   async Task PasteWhatTheClipboardHolds()
   {
      try
      {
         switch(await WhatCtrlVPastes.From(TopLevel.GetTopLevel(this)!.Clipboard!))
         {
            case WhatCtrlVPastes.Text:
               await PasteAsync();
               break;
            case WhatCtrlVPastes.AnImage image:
               await Paste(image.Image);
               break;
         }
      }
      catch(Exception failure)
      {
         PasteFailed?.Invoke(failure);
      }
   }

   /// <summary>
   /// Pastes <paramref name="image"/> as its file's path, alone: as a bracketed paste, which Claude Code takes for the
   /// image, once the program in the terminal has asked for those, as Claude Code does; else as the path, as a shell
   /// takes it.
   /// </summary>
   Task Paste(Bitmap image)
   {
      var file = _pastedImages.Save(image).FullName;
      return SendInputAsync(Terminal.BracketedPasteMode ? StartOfABracketedPaste + file + EndOfABracketedPaste : file);
   }

   /// <summary>Keeps <paramref name="key"/>, pressed, from the terminal, which Cloyd types something else for, and its release with it.</summary>
   void TakeFromTheTerminal(KeyEventArgs key)
   {
      key.Handled = true;
      _heldKeyTakenFromTheTerminal = key.Key;
   }

   /// <summary>Keeps the release of a key taken from the terminal from it too, as it saw no press of the key.</summary>
   void LetGoOfAKeyTakenFromTheTerminal(object? sender, KeyEventArgs key)
   {
      if(key.Key != _heldKeyTakenFromTheTerminal) return;
      key.Handled = true;
      _heldKeyTakenFromTheTerminal = null;
   }

   /// <summary>
   /// Opens the URL of a link Magnus Ctrl+clicked in the browser, when it's a web page's: Iciclecreek's terminal never
   /// opens one itself. Only <c>http</c> and <c>https</c>: a link Claude Code's output writes itself (OSC 8) can have
   /// any scheme, such as <c>file:</c> for a program, or <c>ms-settings:</c>.
   /// </summary>
   /// <remarks>Not specified: Avalonia's headless platform opens nothing, and has no way to see what would be opened.</remarks>
   void OpenAWebPagesUrlInTheBrowser(object? sender, UrlClickedEventArgs click)
   {
      if(Uri.TryCreate(click.Url, UriKind.Absolute, out var url) && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp))
         _ = TopLevel.GetTopLevel(this)!.Launcher.LaunchUriAsync(url);
   }

   /// <summary>
   /// The session host, as the pseudo console the control would run a process in itself: it reads what the host draws,
   /// types into the host, and resizes the host's console. The host's processes are no process of Cloyd's, so it tells
   /// of none, but it has exited, as a process would, once the attachment has ended.
   /// </summary>
   /// <remarks>
   /// The control waits for the process to exit once what it draws has ended, calling <see cref="WaitForExit"/> again and
   /// again for up to 30 s, and counting on each call to wait as long as it asks: until 2026-11-20 it answered at once,
   /// and each terminal whose attachment ended kept a processor core busy for 30 s.
   /// </remarks>
   sealed class SessionHostAsAPseudoConsole : IPtyConnection
   {
      readonly AttachmentToASessionHost _attachment;

      public SessionHostAsAPseudoConsole(AttachmentToASessionHost attachment) => _attachment = attachment;

      public event EventHandler<PtyExitedEventArgs>? ProcessExited { add {} remove {} }
      public Stream ReaderStream => _attachment.Drawn;
      public bool SupportsCancellableRead => true;
      public Stream WriterStream => _attachment.Typed;
      public int Pid => 0;
      public int ExitCode => 0;
      public int ExitSignal => 0;
      public bool HasExited => _attachment.EndsWithin(TimeSpan.Zero);
      public bool WaitForExit(int milliseconds) => _attachment.EndsWithin(TimeSpan.FromMilliseconds(milliseconds));
      public void Resize(int columns, int rows) => _attachment.Resize(new ConsoleSize((short)columns, (short)rows));

      /// <summary>Lets go of the host, as killing would end a process of the control's own: the session runs on.</summary>
      public void Kill() => _attachment.Dispose();

      public Task KillAsync(bool entireProcessTree, TimeSpan? timeout)
      {
         Kill();
         return Task.CompletedTask;
      }

      /// <summary>Nothing: the <see cref="Sessions.SessionTab"/> that shows the host lets go of it.</summary>
      public void Dispose() {}
   }
}
