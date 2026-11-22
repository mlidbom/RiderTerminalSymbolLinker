using System.Reflection;

namespace Cloyd.Diagnostics;

/// <summary>Writes a separate report for each unhandled managed exception in a Cloyd process.
/// The CLR still terminates the process and can produce its Windows crash report.</summary>
sealed class CrashReports(DirectoryInfo folder)
{
   /// <summary>Register before starting the application or running a command, so startup and worker
   /// thread failures reach the same reporter as an exception on the window's UI thread.</summary>
   public void Register() => AppDomain.CurrentDomain.UnhandledException += (_, crash) => Write(crash.ExceptionObject);

   public void Write(object exception)
   {
      try
      {
         var utc = DateTime.UtcNow;
         var build = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
         var report = $"""
                       Cloyd {build}
                       UTC: {utc:O}
                       Process: {Environment.ProcessId}
                       Managed thread: {Environment.CurrentManagedThreadId}

                       {exception}
                       """;
         folder.Create();
         var name = $"crash-{utc:yyyyMMddTHHmmssfffffffZ}-{Environment.ProcessId}-{Guid.NewGuid():N}.log";
         File.WriteAllText(Path.Combine(folder.FullName, name), report);
      }
      catch(Exception writingFailed)
      {
         Console.Error.WriteLine($"Could not write a crash report in {folder.FullName}. {writingFailed}");
         Console.Error.WriteLine(exception);
      }
   }
}
