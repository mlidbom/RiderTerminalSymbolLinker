using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Cloyd.Specifications.TheSandbox;

/// <summary>
/// A job object such as an IDE may run in: everything in it ends as it closes, as with its IDE, and it lets a process
/// started in it break away from it, where the process asks to. The specifications run <c>Cloyd.exe</c> in it, as an IDE
/// runs it for a project it opens.
/// </summary>
/// <remarks>
/// Rider runs in a job, and so does what it starts: <c>~/.claude/dev_docs/sessions-that-outlive-the-ide.md</c>. Which
/// limits its job has could not be read: the process that holds it can't be opened without elevation.
/// </remarks>
sealed unsafe partial class JobOfAnIde : IDisposable
{
   const uint KillOnJobClose = 0x00002000, BreakawayOk = 0x00000800;
   const int JobObjectExtendedLimitInformation = 9;
   const uint CreateSuspended = 0x00000004, CreateUnicodeEnvironment = 0x00000400;
   const uint WaitTimeout = 0x00000102;

   static readonly TimeSpan HowLongRunningMayTake = TimeSpan.FromSeconds(60);

   readonly nint _job;

   public JobOfAnIde()
   {
      _job = CreateJobObjectW(null, null);
      if(_job == 0) throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not create a job.");
      var limits = new ExtendedLimitInformation { LimitFlags = KillOnJobClose | BreakawayOk };
      if(!SetInformationJobObject(_job, JobObjectExtendedLimitInformation, &limits, (uint)sizeof(ExtendedLimitInformation)))
         throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not set the job's limits.");
   }

   /// <summary>
   /// Runs <paramref name="program"/> with <paramref name="arguments"/> in the job, in this process's environment with
   /// <paramref name="variables"/> on top, and returns its exit code once it has exited: in the job from its first
   /// instruction, as it is created suspended and put in the job before it runs. A <see cref="TimeoutException"/> when it
   /// runs longer than <see cref="HowLongRunningMayTake"/>.
   /// </summary>
   public int Run(string program, IEnumerable<string> arguments, IReadOnlyDictionary<string, string> variables)
   {
      var commandLine = $"{string.Join(' ', new[] { program }.Concat(arguments).Select(it => $"\"{it}\""))}\0".ToCharArray();
      var environment = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>().ToDictionary(it => (string)it.Key, it => (string)it.Value!, StringComparer.OrdinalIgnoreCase);
      foreach(var (variable, value) in variables) environment[variable] = value;
      var environmentBlock = string.Concat(environment.OrderBy(it => it.Key, StringComparer.OrdinalIgnoreCase).Select(it => $"{it.Key}={it.Value}\0")) + "\0";
      var startup = new StartupInfo { Size = (uint)sizeof(StartupInfo) };
      fixed(char* writableCommandLine = commandLine)
      fixed(char* environmentOfIt = environmentBlock)
      {
         if(!CreateProcessW(program, writableCommandLine, null, null, false, CreateSuspended | CreateUnicodeEnvironment, environmentOfIt, Path.GetDirectoryName(program)!, &startup, out var started))
            throw new Win32Exception(Marshal.GetLastPInvokeError(), $"Could not start {program}.");
         try
         {
            if(!AssignProcessToJobObject(_job, started.Process)) throw new Win32Exception(Marshal.GetLastPInvokeError(), $"Could not put {program} in the job.");
            ResumeThread(started.Thread);
            if(WaitForSingleObject(started.Process, (uint)HowLongRunningMayTake.TotalMilliseconds) == WaitTimeout)
               throw new TimeoutException($"{program} ran longer than {HowLongRunningMayTake.TotalSeconds:0} s. It ends as the job closes.");
            GetExitCodeProcess(started.Process, out var exitCode);
            return (int)exitCode;
         }
         finally
         {
            CloseHandle(started.Process);
            CloseHandle(started.Thread);
         }
      }
   }

   /// <summary>Whether the process <paramref name="processId"/> runs in any job.</summary>
   public static bool RunsInAJob(int processId)
   {
      var process = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, (uint)processId);
      if(process == 0) throw new Win32Exception(Marshal.GetLastPInvokeError(), $"Could not open process {processId}.");
      try
      {
         if(!IsProcessInJob(process, 0, out var inAJob)) throw new Win32Exception(Marshal.GetLastPInvokeError(), $"Could not tell whether process {processId} runs in a job.");
         return inAJob;
      }
      finally
      {
         CloseHandle(process);
      }
   }

   /// <summary>Closes the job, as its IDE exiting does: everything still in it ends.</summary>
   public void Dispose() => CloseHandle(_job);

   [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
   private static partial nint CreateJobObjectW(void* attributes, string? name);

   [LibraryImport("kernel32.dll", SetLastError = true)]
   [return: MarshalAs(UnmanagedType.Bool)]
   private static partial bool SetInformationJobObject(nint job, int informationClass, void* information, uint length);

   [LibraryImport("kernel32.dll", SetLastError = true)]
   [return: MarshalAs(UnmanagedType.Bool)]
   private static partial bool AssignProcessToJobObject(nint job, nint process);

   [LibraryImport("kernel32.dll", SetLastError = true)]
   [return: MarshalAs(UnmanagedType.Bool)]
   private static partial bool IsProcessInJob(nint process, nint job, [MarshalAs(UnmanagedType.Bool)] out bool result);

   [LibraryImport("kernel32.dll", SetLastError = true)]
   private static partial nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

   [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
   [return: MarshalAs(UnmanagedType.Bool)]
   private static partial bool CreateProcessW(string applicationName,
                                              char* commandLine,
                                              void* processAttributes,
                                              void* threadAttributes,
                                              [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
                                              uint creationFlags,
                                              char* environment,
                                              string currentDirectory,
                                              StartupInfo* startupInfo,
                                              out ProcessInformation processInformation);

   [LibraryImport("kernel32.dll")]
   private static partial uint ResumeThread(nint thread);

   [LibraryImport("kernel32.dll")]
   private static partial uint WaitForSingleObject(nint handle, uint milliseconds);

   [LibraryImport("kernel32.dll")]
   [return: MarshalAs(UnmanagedType.Bool)]
   private static partial bool GetExitCodeProcess(nint process, out uint exitCode);

   [LibraryImport("kernel32.dll")]
   [return: MarshalAs(UnmanagedType.Bool)]
   private static partial bool CloseHandle(nint handle);

   /// <summary>Windows' <c>JOBOBJECT_EXTENDED_LIMIT_INFORMATION</c>: only <see cref="LimitFlags"/> set.</summary>
   [StructLayout(LayoutKind.Sequential)]
   struct ExtendedLimitInformation
   {
      readonly long _perProcessUserTimeLimit, _perJobUserTimeLimit;
      public uint LimitFlags;
      readonly nuint _minimumWorkingSetSize, _maximumWorkingSetSize;
      readonly uint _activeProcessLimit;
      readonly nuint _affinity;
      readonly uint _priorityClass, _schedulingClass;
      readonly ulong _readOperations, _writeOperations, _otherOperations, _readBytes, _writeBytes, _otherBytes;
      readonly nuint _processMemoryLimit, _jobMemoryLimit, _peakProcessMemoryUsed, _peakJobMemoryUsed;
   }

   /// <summary>Windows' <c>STARTUPINFOW</c>, all zeros but its size.</summary>
   [StructLayout(LayoutKind.Sequential)]
   struct StartupInfo
   {
      public uint Size;
      readonly nint _reserved, _desktop, _title;
      readonly uint _x, _y, _width, _height, _columns, _rows, _fillAttribute, _flags;
      readonly ushort _showWindow, _sizeOfReserved2;
      readonly nint _reserved2, _standardInput, _standardOutput, _standardError;
   }

   /// <summary>Windows' <c>PROCESS_INFORMATION</c>: what <c>CreateProcessW</c> started.</summary>
   [StructLayout(LayoutKind.Sequential)]
   readonly struct ProcessInformation
   {
      public readonly nint Process, Thread;
      readonly uint _processId, _threadId;
   }
}
