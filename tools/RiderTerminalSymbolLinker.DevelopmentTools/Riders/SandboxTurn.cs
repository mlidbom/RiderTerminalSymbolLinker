using System.Diagnostics;
using System.Text;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   A turn at running sandbox Riders on this machine: one process holds it at a time, and every other process that
///   takes it waits until it ends. A run of the specifications holds it from its build to its sandbox's exit, and the
///   development tools' commands that start, stop or install the sandbox hold it while they run. The .NET
///   Analysis&amp;Refactoring MCP's specifications and tools take the same turn, through <see cref="TheFile"/>.
/// </summary>
/// <remarks>
///   <para>
///     A run of the specifications starts a full Rider, gigabytes and several cores, and the MCP's start one or two.
///     Several suites at once, beside Magnus's own IDEs, froze his machine (2026-11-17). Magnus wants one suite of
///     sandbox Riders running at a time (2026-11-18).
///   </para>
///   <para>
///     A turn is <see cref="TheFile"/> held open for writing, which other processes may then only read. In it is a line
///     saying who holds it, for the messages of those who wait. The operating system closes the file when its process
///     dies, so a run that crashed or was killed ends its turn. Any thread may close it, which an xUnit fixture needs: it
///     may be disposed on another thread than the one that constructed it. A system mutex, such as Compze's
///     <c>IMutex</c>, can only be released by the thread that took it.
///   </para>
///   <para>
///     A copy of the MCP's <c>DotNetAnalysisAndRefactoringMcp.DevelopmentEnvironment.Riders.SandboxTurn</c>
///     (resharper-mcp commit 86124b1): both must open <see cref="TheFile"/> exactly as they do now, or they don't
///     exclude each other.
///   </para>
///   <para>
///     todo:compze: compze-agent-rep proposed <c>IProcessLock</c> for Compze.Threading.Interprocess: a lock held by its
///     holder rather than a thread, released by the operating system when its process dies, over a file held open for
///     writing, as this turn is. If Compze gets it, replace this class with it, at the same time as the MCP's session
///     replaces its copy (Magnus, 2026-11-18).
///   </para>
/// </remarks>
public sealed class SandboxTurn : IDisposable
{
   /// <summary>
   ///   How long a process waits for the turn before it gives up. A run takes one to two minutes, but several may be
   ///   waiting, or one may hang until its own timeouts give up.
   /// </summary>
   static readonly TimeSpan HowLongToWait = TimeSpan.FromMinutes(20);

   static readonly TimeSpan HowOftenToTry = TimeSpan.FromMilliseconds(500);

   /// <summary>The HRESULT of an <see cref="IOException"/> for a file that another handle has open in a way that excludes ours.</summary>
   const int SharingViolation = unchecked((int)0x80070020);

   readonly FileStream _heldOpen;

   SandboxTurn(FileStream heldOpen) => _heldOpen = heldOpen;

   /// <summary>
   ///   The file that a holder of the turn keeps open for writing and shares only for reading: every suite that takes
   ///   the turn opens this path so.
   /// </summary>
   public static string TheFile { get; } =
      Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SandboxRiders", "turn.lock");

   /// <summary>
   ///   Waits until no other process holds the turn, then takes it for <paramref name="whatItIsFor"/>, which the
   ///   processes that wait meanwhile name in their messages.
   /// </summary>
   public static SandboxTurn Take(string whatItIsFor)
   {
      Directory.CreateDirectory(Path.GetDirectoryName(TheFile)!);
      var turn = TryToTake(whatItIsFor);
      if(turn is null) Console.Error.WriteLine($"Waiting for the sandbox turn, which {WhoHoldsIt()} holds.");

      var waiting = Stopwatch.StartNew();
      while(turn is null)
      {
         if(waiting.Elapsed > HowLongToWait)
            throw new TimeoutException($"Gave up after {HowLongToWait} waiting for the sandbox turn ({TheFile}), which {WhoHoldsIt()} holds.");
         Thread.Sleep(HowOftenToTry);
         turn = TryToTake(whatItIsFor);
      }

      return turn;
   }

   /// <summary>Ends the turn: a process waiting for it takes it at its next try.</summary>
   public void Dispose() => _heldOpen.Dispose();

   static SandboxTurn? TryToTake(string whatItIsFor)
   {
      FileStream file;
      try
      {
         file = new FileStream(TheFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
      }
      catch(IOException exception) when(exception.HResult == SharingViolation)
      {
         return null;
      }

      file.SetLength(0);
      file.Write(Encoding.UTF8.GetBytes($"{whatItIsFor}, process {Environment.ProcessId}, since {DateTime.Now:yyyy-MM-dd HH:mm:ss}"));
      file.Flush();
      return new SandboxTurn(file);
   }

   /// <summary>The line that the holder wrote in <see cref="TheFile"/>.</summary>
   /// <remarks>Empty for a moment after the holder opened the file, before it wrote the line.</remarks>
   static string WhoHoldsIt()
   {
      using var reader = new StreamReader(new FileStream(TheFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
      return reader.ReadToEnd() is { Length: > 0 } line ? line : "a process that hasn't yet written who it is";
   }
}
