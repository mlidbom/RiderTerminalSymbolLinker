using System.Reflection;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

public class When_Cloyd_writes_a_crash_report : IDisposable
{
   readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("cloyd-crash-reports-");

   [XF] public void each_failure_keeps_its_build_thread_and_complete_exception_in_its_own_file()
   {
      var reports = new CloydsFolder(_folder).CrashReports;
      for(var i = 0; i < 2; i++)
      {
         try { throw new InvalidOperationException("Outer failure", new IOException("Inner cause")); }
         catch(InvalidOperationException failure) { reports.Write(failure); }
      }
      var files = new DirectoryInfo(Path.Combine(_folder.FullName, "crashes")).GetFiles("*.log");
      files.Length.Must().Be(2);
      var build = typeof(CloydsFolder).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
      foreach(var file in files)
      {
         var report = File.ReadAllText(file.FullName);
         foreach(var expected in new[] { $"Cloyd {build}", "UTC: ", $"Process: {Environment.ProcessId}", $"Managed thread: {Environment.CurrentManagedThreadId}",
                                         "System.InvalidOperationException: Outer failure", "System.IO.IOException: Inner cause", nameof(When_Cloyd_writes_a_crash_report) })
            report.Must().Contain(expected);
      }
   }

   public void Dispose() => _folder.Delete(recursive: true);
}
