using AgentSpace.Avalonia.XTerm;
using Avalonia.Controls;
using Avalonia.Media;
using ClaudeTooling.SessionHost.Protocol;
using CloydSpike.SessionHosts;

namespace CloydSpike.TuiHosts;

/// <summary>AgentSpace.Avalonia.XTerm's control, on its own VT parser, driven from the session host as a custom <see cref="ITerminalConnection"/>.</summary>
sealed class AgentSpaceTuiHost : TuiHost
{
   public override string Name => "AgentSpace.Avalonia.XTerm";

   public override Control Showing(AttachmentToASessionHost attachment) =>
      new XTermControl { FontFamily = new FontFamily("Cascadia Mono"), FontSize = 14, Connection = new SessionHostAsAConnection(attachment) };

   sealed class SessionHostAsAConnection(AttachmentToASessionHost attachment) : ITerminalConnection
   {
      public Stream ReaderStream => attachment.Drawn;
      public Stream WriterStream => attachment.Typed;
      public void Resize(int cols, int rows) => attachment.Resize(new ConsoleSize((short)cols, (short)rows));
      public ValueTask DisposeAsync() => ValueTask.CompletedTask;
   }
}
