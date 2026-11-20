using System.Diagnostics;
using Compze.Must;
using Compze.xUnitBDD;
using Cloyd.LocalApi;
using Cloyd.Specifications.TheSandbox;

namespace Cloyd.Specifications;

/// <summary>
/// No Cloyd application runs, and <c>Cloyd.exe --project &lt;folder&gt;</c> is run in a job that ends everything in it as
/// it closes, and lets what is started in it break away, as an IDE runs it for a project it opens. Then the job closes,
/// as when the IDE exits.
/// </summary>
/// <remarks>
/// The real <c>Cloyd.exe</c>, beside the specifications' assembly, in processes of its own: its application shows a real
/// window on the desktop for a few seconds. It serves a <see cref="CloydsFolder"/> of the specification's own, and
/// reaches the sandbox's hub, whose <see cref="SandboxHub.Environment"/> it takes from the specifications' process.
/// </remarks>
public class When_Cloyd_exe_is_run_for_a_project_in_a_job_that_ends_everything_in_it_as_it_closes : IDisposable
{
   readonly SandboxProject _project = SandboxProject.Named(nameof(When_Cloyd_exe_is_run_for_a_project_in_a_job_that_ends_everything_in_it_as_it_closes));
   readonly SandboxProject _anotherProject = SandboxProject.Named($"{nameof(When_Cloyd_exe_is_run_for_a_project_in_a_job_that_ends_everything_in_it_as_it_closes)}.AnotherProject");
   readonly CloydsFolder _cloydsFolder = Sandbox.Started.EmptyCloydsFolder(nameof(When_Cloyd_exe_is_run_for_a_project_in_a_job_that_ends_everything_in_it_as_it_closes));
   readonly TheCloydApplication _application;
   readonly int _exitCode;
   readonly int _processOfTheApplication;

   public When_Cloyd_exe_is_run_for_a_project_in_a_job_that_ends_everything_in_it_as_it_closes()
   {
      _application = new TheCloydApplication(_cloydsFolder);
      using(var job = new JobOfAnIde()) _exitCode = CloydExeRunFor(_project, job);
      _processOfTheApplication = _application.ProcessId ?? throw new InvalidOperationException($"No Cloyd application answers at the announcement in {_cloydsFolder}.");
   }

   /// <summary>The exit code of <c>Cloyd.exe --project</c>, run for <paramref name="project"/> in <paramref name="job"/>.</summary>
   int CloydExeRunFor(SandboxProject project, JobOfAnIde job) =>
      job.Run(Path.Combine(AppContext.BaseDirectory, "Cloyd.exe"), ["--project", project.Project.Folder], new Dictionary<string, string> { [CloydsFolder.VariableNamingAnother] = _cloydsFolder.Folder.FullName });

   [XF] public void the_command_succeeds() => _exitCode.Must().Be(0);

   [XF] public void the_application_shows_the_projects_window() => _application.Windows()!.Select(it => it.Project).Must().SequenceEqual([_project.Project.Folder]);

   [XF] public void the_application_runs_outside_every_job() => JobOfAnIde.RunsInAJob(_processOfTheApplication).Must().BeFalse();

   public class and_then_for_another_project : When_Cloyd_exe_is_run_for_a_project_in_a_job_that_ends_everything_in_it_as_it_closes
   {
      readonly int _exitCodeAgain;

      public and_then_for_another_project()
      {
         using var job = new JobOfAnIde();
         _exitCodeAgain = CloydExeRunFor(_anotherProject, job);
      }

      [XF] public void the_command_succeeds_again() => _exitCodeAgain.Must().Be(0);

      [XF] public void the_same_application_shows_both_windows()
      {
         _application.ProcessId.Must().Be(_processOfTheApplication);
         _application.Windows()!.Select(it => it.Project).Must().SequenceEqual([_project.Project.Folder, _anotherProject.Project.Folder]);
      }
   }

   public void Dispose()
   {
      using(var application = Process.GetProcessById(_processOfTheApplication))
      {
         application.Kill();
         application.WaitForExit();
      }

      _project.Dispose();
      _anotherProject.Dispose();
      GC.SuppressFinalize(this);
   }
}
