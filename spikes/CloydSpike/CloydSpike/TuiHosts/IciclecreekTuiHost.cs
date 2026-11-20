using Avalonia.Controls;
using Avalonia.Media;
using ClaudeTooling.SessionHost.Protocol;
using CloydSpike.SessionHosts;
using Iciclecreek.Terminal;
using Porta.Pty;

namespace CloydSpike.TuiHosts;

/// <summary>Iciclecreek.Avalonia.Terminal's control, on XTerm.NET, driven from the session host through its <c>AttachConnection</c>.</summary>
sealed class IciclecreekTuiHost : TuiHost
{
   public override string Name => "Iciclecreek.Avalonia.Terminal";

   public override Control Showing(AttachmentToASessionHost attachment)
   {
      var terminal = new TerminalControl { FontFamily = new FontFamily("Cascadia Mono"), FontSize = 14, BufferSize = 5000 };
      terminal.TemplateApplied += (_, _) => terminal.AttachConnection(new SessionHostAsAPty(attachment));
      return terminal;
   }

   /// <summary>The session host as the pseudo console Porta.Pty would give the control: its process is the host's.</summary>
   sealed class SessionHostAsAPty(AttachmentToASessionHost attachment) : IPtyConnection
   {
      public event EventHandler<PtyExitedEventArgs>? ProcessExited { add { } remove { } }
      public Stream ReaderStream => attachment.Drawn;
      public bool SupportsCancellableRead => true;
      public Stream WriterStream => attachment.Typed;
      public int Pid => 0;
      public int ExitCode => 0;
      public int ExitSignal => 0;
      public bool HasExited => false;
      public bool WaitForExit(int milliseconds) => false;
      public void Kill() => attachment.Dispose();
      public Task KillAsync(bool entireProcessTree, TimeSpan? timeout)
      {
         Kill();
         return Task.CompletedTask;
      }
      public void Resize(int columns, int rows) => attachment.Resize(new ConsoleSize((short)columns, (short)rows));
      public void Dispose() { }
   }
}
