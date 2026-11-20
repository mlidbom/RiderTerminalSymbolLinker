using System.Buffers;
using System.IO.Pipelines;
using ClaudeTooling.SessionHost.Protocol;

namespace CloydSpike.SessionHosts;

/// <summary>
/// The spike's attach client, in process: a connection to a session host's pipe as an attach client, as
/// <c>ClaudeTooling.SessionHost.exe attach</c> makes one, so that a terminal control in the window draws what the host's
/// pseudo console draws, with no console of its own in between.
/// </summary>
/// <remarks>
/// Attaching takes the session over from the attach client before it, as a Rider tab's: the spike attaches only to hosts
/// it started (<see cref="Hub.SessionHostOfTheHub.IsTheSpikes"/>).
/// </remarks>
sealed class AttachmentToASessionHost : IDisposable
{
   readonly ConnectionToASessionHost _connection;
   readonly Pipe _drawn = new(new PipeOptions(pauseWriterThreshold: 4 << 20, resumeWriterThreshold: 2 << 20));

   AttachmentToASessionHost(ConnectionToASessionHost connection, ConsoleSize size)
   {
      _connection = connection;
      Size = size;
      Drawn = _drawn.Reader.AsStream();
      Typed = new StreamTypedIntoTheHost(this);
   }

   /// <summary>What the host's pseudo console draws, as VT sequences in UTF-8, from the terminal modes it replays first on.</summary>
   public Stream Drawn { get; }

   /// <summary>What is written to it goes to the host as typed: VT sequences in UTF-8.</summary>
   public Stream Typed { get; }

   /// <summary>The size the host's pseudo console was last given.</summary>
   public ConsoleSize Size { get; private set; }

   /// <summary>Raised with each piece typed into the host, on the thread that typed it: for the probe's log of what each candidate sends.</summary>
   public event Action<byte[]>? WasTyped;

   /// <summary>Raised once, on the thread that relays, when the host lets the attachment go: why.</summary>
   public event Action<string>? Ended;

   /// <summary>Attaches to the host with the id <paramref name="host"/>, which resizes its pseudo console to <paramref name="size"/> and draws it all again.</summary>
   public static AttachmentToASessionHost To(SessionHostId host, ConsoleSize size)
   {
      var connection = ConnectionToASessionHost.TryConnect(host, TimeSpan.FromSeconds(5)) ?? throw new InvalidOperationException($"No session host listens as {host}.");
      connection.Send(SessionHostFrame.Hello(HelloToTheSessionHost.OfAnAttachClient(size)));
      var attachment = new AttachmentToASessionHost(connection, size);
      new Thread(attachment.RelayWhatTheHostSends) { IsBackground = true, Name = $"Relaying session host {host}" }.Start();
      return attachment;
   }

   public void Type(ReadOnlySpan<byte> typed)
   {
      var bytes = typed.ToArray();
      WasTyped?.Invoke(bytes);
      _connection.TrySend(SessionHostFrame.Input(bytes));
   }

   public void Resize(ConsoleSize size)
   {
      if(size == Size || !size.IsASize) return;

      Size = size;
      _connection.TrySend(SessionHostFrame.Size(size));
   }

   public void Dispose() => _connection.Dispose();

   /// <summary>The relay thread's entry point: it ends with the reason, whatever ends it.</summary>
   void RelayWhatTheHostSends()
   {
      string why;
      try
      {
         why = RelayUntilLetGo();
      }
      catch(Exception failure)
      {
         why = $"the connection failed: {failure.Message}";
      }

      _drawn.Writer.Complete();
      Ended?.Invoke(why);
   }

   string RelayUntilLetGo()
   {
      while(_connection.NextFrame() is { } frame)
      {
         switch(frame.Kind)
         {
            case KindOfSessionHostFrame.Output:
               _drawn.Writer.Write(frame.Payload);
               _drawn.Writer.FlushAsync().AsTask().GetAwaiter().GetResult();
               break;
            case KindOfSessionHostFrame.TakenOver:
               return "another attach client took the session over";
            case KindOfSessionHostFrame.Ended:
               return $"the shell exited, with {frame.ExitCodeOfTheShell}";
         }
      }

      return "the host closed the connection";
   }

   /// <summary>A stream that types what is written to it into the host.</summary>
   sealed class StreamTypedIntoTheHost(AttachmentToASessionHost attachment) : Stream
   {
      public override bool CanRead => false;
      public override bool CanSeek => false;
      public override bool CanWrite => true;
      public override long Length => throw new NotSupportedException();
      public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

      public override void Write(byte[] buffer, int offset, int count) => attachment.Type(buffer.AsSpan(offset, count));
      public override void Write(ReadOnlySpan<byte> buffer) => attachment.Type(buffer);
      public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellation)
      {
         Write(buffer, offset, count);
         return Task.CompletedTask;
      }
      public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellation = default)
      {
         Write(buffer.Span);
         return ValueTask.CompletedTask;
      }
      public override void Flush() { }
      public override Task FlushAsync(CancellationToken cancellation) => Task.CompletedTask;
      public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
      public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
      public override void SetLength(long value) => throw new NotSupportedException();
   }
}
