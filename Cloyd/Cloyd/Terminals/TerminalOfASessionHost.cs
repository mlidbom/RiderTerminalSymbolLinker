using Avalonia.Media;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.SessionHosts;
using Iciclecreek.Terminal;
using Porta.Pty;

namespace Cloyd.Terminals;

/// <summary>
/// The terminal of a session host: Iciclecreek's terminal control, on XTerm.NET, drawing what the host's pseudo console
/// draws and typing into it, as a terminal does for a pseudo console of its own. It speaks the console's win32 input
/// mode, which the host's console asks for, so that each key reaches Claude Code exactly, Escape included.
/// </summary>
sealed class TerminalOfASessionHost : TerminalControl
{
   /// <summary>How many lines scrolled off the top it keeps.</summary>
   const int LinesItKeeps = 5000;

   bool _attached;

   public TerminalOfASessionHost(AttachmentToASessionHost attachment)
   {
      FontFamily = new FontFamily("Cascadia Mono");
      FontSize = 14;
      BufferSize = LinesItKeeps;
      TemplateApplied += (_, _) =>
      {
         if(_attached) return;
         _attached = true;
         AttachConnection(new SessionHostAsAPseudoConsole(attachment));
      };
   }

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
   /// The session host, as the pseudo console the control would run a process in itself: it reads what the host draws,
   /// types into the host, and resizes the host's console. The host's processes are no process of Cloyd's, so it tells
   /// of none.
   /// </summary>
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
      public bool HasExited => false;
      public bool WaitForExit(int milliseconds) => false;
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
