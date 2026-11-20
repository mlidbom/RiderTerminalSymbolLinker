using System.Diagnostics;
using ClaudeTooling.SessionHost.Protocol;
using Compze.Must;
using Compze.xUnitBDD;
using Xunit;
using Cloyd.Projects;
using Cloyd.SessionHosts;
using Cloyd.Sessions;
using Cloyd.Specifications.TheSandbox;

namespace Cloyd.Specifications;

/// <summary>
/// A session's tab is let go by its host, for one of the reasons its nested classes give. The facts of this class are
/// xUnit's own <see cref="FactAttribute"/>s, which run in each nested class, as an <see cref="XFAttribute"/> runs only in
/// the class that declares it, and this one has no instance.
/// </summary>
public abstract class When_a_sessions_tab_is_let_go_by_its_host : SpecificationOpeningWindows
{
   readonly SandboxProject _project;
   readonly ProjectWindow _window;
   readonly SessionTab _tab;

   protected When_a_sessions_tab_is_let_go_by_its_host()
   {
      _project = Sandbox.Project("the-project");
      _window = Open(_project.Window);
      DoneOnTheUIThread(() => _window.StartANewSession(null));
      _tab = OnTheUIThread(() => _window.Tabs.Single());
      WaitUntil(() => _tab.SessionRunningInIt is not null, "Claude Code ran in the tab's host");
   }

   [Fact] public void the_tab_closes() => WaitUntil(() => _window.Tabs.Count == 0, "the tab had closed");

   [Fact] public void the_window_tells_of_no_failure()
   {
      WaitUntil(() => _window.Tabs.Count == 0, "the tab had closed");
      OnTheUIThread(() => _window.Failure).Must().BeNull();
   }

   /// <summary>
   /// The processor time the specifications' process takes in the second after the tab has closed, once no sandbox is
   /// starting or ending in the background, which nothing else asks of it: its terminal, which waits for the pseudo
   /// console it showed to exit, took a core of its own for 30 s until 2026-11-20.
   /// </summary>
   [Fact] public void its_terminal_keeps_no_processor_busy()
   {
      WaitUntil(() => _window.Tabs.Count == 0, "the tab had closed");
      SandboxesOfThisRun.WaitUntilNoneIsStartingOrEnding();
      using var process = Process.GetCurrentProcess();
      var before = process.TotalProcessorTime;
      Thread.Sleep(TimeSpan.FromSeconds(1));
      process.Refresh();
      (process.TotalProcessorTime - before).Must().Satisfy(it => it < TimeSpan.FromMilliseconds(300));
   }

   public class because_another_attach_client_took_the_session_over : When_a_sessions_tab_is_let_go_by_its_host
   {
      public because_another_attach_client_took_the_session_over() => EndWithTheScenario(AttachmentToASessionHost.To(_tab.Host.Id, ConsoleSize.Default));

      [XF] public void the_session_runs_on_in_its_host()
      {
         WaitUntil(() => _window.Tabs.Count == 0, "the tab had closed");
         _project.SessionRunningIn(_tab.Host.Id).Must().NotBeNull();
      }
   }

   public class because_its_shell_exited : When_a_sessions_tab_is_let_go_by_its_host
   {
      public because_its_shell_exited()
      {
         using var shell = Process.GetProcessById(_tab.Host.ShellProcessId);
         shell.Kill();
      }

      [XF] public void the_host_has_ended() => WaitUntil(() => _project.SessionHost(_tab.Host.Id) is null, "the hub no longer listed the host whose shell exited");
   }
}
