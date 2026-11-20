using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Cloyd.LocalApi;

/// <summary>
/// Starting the Cloyd application, for <c>Cloyd.exe --project</c> when none runs: <c>Cloyd.exe --application
/// &lt;Cloyd's folder&gt;</c>, so that it outlives the command, and the IDE that ran the command.
/// </summary>
/// <remarks>
/// An IDE runs in a job object, and so does what it starts, and a job may end everything in it as it closes. So the
/// application is started breaking away from the jobs the command runs in, where they let it. A job that lets no process
/// break away fails the start with <c>ERROR_ACCESS_DENIED</c>: then it is started as the hub is started, detached, which
/// a job that lets its processes' children break away silently lets out too, and else keeps.<br/>
/// Detached, in a process group of its own, with no console and no handle of the command's, as the hub is started
/// (<c>~/.claude/src/ClaudeTooling.Hub.Protocol/StartingTheHub.cs</c>). It runs in its executable's folder, so that it
/// holds no folder a command was run in, which then couldn't be deleted.
/// </remarks>
static unsafe partial class StartingTheApplication
{
   const uint DetachedInAGroupOfItsOwn = 0x00000008 /* DETACHED_PROCESS */ | 0x00000200 /* CREATE_NEW_PROCESS_GROUP */;

   /// <summary><c>CREATE_BREAKAWAY_FROM_JOB</c>: outside the jobs of the process that starts it, where each lets it.</summary>
   const uint BreakingAwayFromItsJobs = 0x01000000;

   const int ErrorAccessDenied = 5;

   /// <summary>The Cloyd.exe beside Cloyd's assembly: the one that runs the command.</summary>
   static string Executable => Path.Combine(AppContext.BaseDirectory, "Cloyd.exe");

   /// <summary>
   /// Starts the application, serving <paramref name="folder"/>, and returns as soon as its process is created: it takes
   /// a while longer to announce its local API.
   /// </summary>
   public static void Start(CloydsFolder folder)
   {
      var commandLine = $"\"{Executable}\" {string.Join(' ', Program.ArgumentsOfTheApplication(folder).Select(it => $"\"{it}\""))}";
      if(StartedWith(commandLine, DetachedInAGroupOfItsOwn | BreakingAwayFromItsJobs) is not {} refused) return;
      if(refused != ErrorAccessDenied) throw new Win32Exception(refused, $"Could not start {commandLine}");
      if(StartedWith(commandLine, DetachedInAGroupOfItsOwn) is {} failed) throw new Win32Exception(failed, $"Could not start {commandLine}");
   }

   /// <summary>Starts <paramref name="commandLine"/> with <paramref name="flags"/>: null once started, else Windows' error.</summary>
   static int? StartedWith(string commandLine, uint flags)
   {
      var writableCommandLine = $"{commandLine}\0".ToCharArray();
      var startup = new StartupInfo { Size = (uint)sizeof(StartupInfo) };
      fixed(char* commandLineToWrite = writableCommandLine)
      {
         if(!CreateProcessW(Executable, commandLineToWrite, null, null, false, flags, null, AppContext.BaseDirectory, &startup, out var started))
            return Marshal.GetLastPInvokeError();
         CloseHandle(started.Process);
         CloseHandle(started.Thread);
         return null;
      }
   }

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
   [return: MarshalAs(UnmanagedType.Bool)]
   private static partial bool CloseHandle(nint handle);

   /// <summary>Windows' <c>STARTUPINFOW</c>, all zeros but its size: no window, title or standard handles of its own to give.</summary>
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
