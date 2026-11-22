using System.IO.Pipes;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.SessionHosts;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

public class When_a_terminal_closes_with_unread_output
{
   [XF] public void the_attachment_stops_even_while_waiting_for_the_terminal_to_read()
   {
      var host = SessionHostId.New();
      using var pipe = new NamedPipeServerStream(host.NameOfThePipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                                                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, 4096, 4096);
      var connected = pipe.WaitForConnectionAsync();
      using var attachment = AttachmentToASessionHost.To(host, ConsoleSize.Default);
      connected.GetAwaiter().GetResult();
      SessionHostFrame.ReadFrom(pipe)!.Kind.Must().Be(KindOfSessionHostFrame.Hello);

      // Leave more than the attachment's 4 MiB limit unread. Receiving the first byte proves the relay
      // is flushing this frame, independently of which thread reaches Dispose first.
      SessionHostFrame.Output(new byte[5 << 20]).WriteTo(pipe);
      attachment.Drawn.ReadByte().Must().Be(0);
      attachment.Dispose();
      attachment.EndsWithin(TimeSpan.FromSeconds(3)).Must().BeTrue();
   }
}
