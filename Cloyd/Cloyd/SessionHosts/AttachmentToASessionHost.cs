using System.Buffers;
using System.IO.Pipelines;
using ClaudeTooling.SessionHost.Protocol;

namespace Cloyd.SessionHosts;

/// <summary>
/// Cloyd's attach client, in its own process: a connection to a session host's pipe as an attach client, as
/// <c>ClaudeTooling.SessionHost.exe attach</c> makes one, so that a terminal in Cloyd's window draws what the host's
/// pseudo console draws, and types into it, with no console between them.
/// </summary>
/// <remarks>
/// The newest attach client takes the session over from the one before: the host then lets this one go, and
/// <see cref="Ended"/> says so. The host first sends each new attach client the terminal modes its programs set, such as
/// the alternate screen and mouse reporting, and then draws its whole screen again.
/// </remarks>
sealed class AttachmentToASessionHost : IDisposable
{
   static readonly TimeSpan HowLongAHostMayTakeToAnswer = TimeSpan.FromSeconds(5);

   readonly ConnectionToASessionHost _connection;

   /// <summary>The thread that relays what the host draws, and ends as the attachment does.</summary>
   readonly Thread _relaying;

   /// <summary>Holds what the host draws until the terminal reads it: the relay waits when the terminal is that far behind.</summary>
   readonly Pipe _drawn = new(new PipeOptions(pauseWriterThreshold: 4 << 20, resumeWriterThreshold: 2 << 20));

   volatile bool _letGo;

   /// <summary>The size the host's pseudo console was last given.</summary>
   ConsoleSize _size;

   AttachmentToASessionHost(SessionHostId host, ConnectionToASessionHost connection, ConsoleSize size)
   {
      _connection = connection;
      _size = size;
      Drawn = _drawn.Reader.AsStream();
      Typed = new StreamTypedIntoTheHost(this);
      _relaying = new Thread(RelayWhatTheHostSends) { IsBackground = true, Name = $"Relaying what session host {host} draws" };
   }

   /// <summary>What the host's pseudo console draws, as VT sequences in UTF-8: the terminal modes first, then the whole screen, then each change.</summary>
   public Stream Drawn { get; }

   /// <summary>What is written to it reaches the host's pseudo console as typed: VT sequences in UTF-8.</summary>
   public Stream Typed { get; }

   /// <summary>
   /// Raised once, on the thread that relays, when the host lets the attachment go, with why, for a human: another attach
   /// client took the session over, or its shell exited, which ended the host. Not raised for <see cref="Dispose"/>.
   /// </summary>
   public event Action<string>? Ended;

   /// <summary>
   /// Whether the attachment has ended, or ends within <paramref name="time"/>: once the host has let it go, or it was
   /// disposed, and all the host drew has been handed on to <see cref="Drawn"/>.
   /// </summary>
   public bool EndsWithin(TimeSpan time) => _relaying.Join(time);

   /// <summary>
   /// Attaches to <paramref name="host"/>, which resizes its pseudo console to <paramref name="size"/> and draws it all
   /// again: an <see cref="InvalidOperationException"/> when no host listens as <paramref name="host"/>.
   /// </summary>
   public static AttachmentToASessionHost To(SessionHostId host, ConsoleSize size)
   {
      var connection = ConnectionToASessionHost.TryConnect(host, HowLongAHostMayTakeToAnswer) ?? throw new InvalidOperationException($"No session host listens as {host}.");
      connection.Send(SessionHostFrame.Hello(HelloToTheSessionHost.OfAnAttachClient(size)));
      var attachment = new AttachmentToASessionHost(host, connection, size);
      attachment._relaying.Start();
      return attachment;
   }

   /// <summary>Resizes the host's pseudo console to <paramref name="size"/>, which has Claude Code draw itself again: nothing for the size it has, or no size at all.</summary>
   public void Resize(ConsoleSize size)
   {
      if(size == _size || !size.IsASize) return;

      _size = size;
      _connection.TrySend(SessionHostFrame.Size(size));
   }

   /// <summary>Lets go of the host, whose session runs on, for the next attach client to show.</summary>
   public void Dispose()
   {
      _letGo = true;
      _connection.Dispose();
   }

   void Type(ReadOnlySpan<byte> typed) => _connection.TrySend(SessionHostFrame.Input(typed.ToArray()));

   /// <summary>The relay thread's entry point, which ends with why the host let the attachment go, whatever ends it.</summary>
   void RelayWhatTheHostSends()
   {
      string why;
      try
      {
         why = RelayUntilLetGo();
      }
      catch(Exception failure)
      {
         why = $"the connection to the session host failed: {failure.Message}";
      }

      _drawn.Writer.Complete();
      if(!_letGo) Ended?.Invoke(why);
   }

   string RelayUntilLetGo()
   {
      while(_connection.NextFrame() is {} frame)
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
               return $"its shell exited, with {frame.ExitCodeOfTheShell}";
         }
      }

      return "the session host closed the connection";
   }

   /// <summary>A stream that types what is written to it into the host: a terminal's input, as a pseudo console's would be.</summary>
   sealed class StreamTypedIntoTheHost : Stream
   {
      readonly AttachmentToASessionHost _attachment;

      public StreamTypedIntoTheHost(AttachmentToASessionHost attachment) => _attachment = attachment;

      public override bool CanRead => false;
      public override bool CanSeek => false;
      public override bool CanWrite => true;
      public override long Length => throw new NotSupportedException();
      public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

      public override void Write(byte[] buffer, int offset, int count) => _attachment.Type(buffer.AsSpan(offset, count));
      public override void Write(ReadOnlySpan<byte> buffer) => _attachment.Type(buffer);

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

      public override void Flush() {}
      public override Task FlushAsync(CancellationToken cancellation) => Task.CompletedTask;
      public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
      public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
      public override void SetLength(long value) => throw new NotSupportedException();
   }
}
