using System.Diagnostics;
using ClaudeTooling.SessionHost.Protocol;
using Compze.Must;
using Compze.xUnitBDD;
using Cloyd.Projects;
using Cloyd.SessionHosts;
using Cloyd.Sessions;
using Cloyd.Specifications.TheSandbox;

namespace Cloyd.Specifications;

public abstract class When_a_sessions_tab_is_let_go_by_its_host : SpecificationOpeningWindows
{
   readonly SandboxProject _project;
   readonly ProjectWindow _window;
   readonly SessionTab _tab;

   protected When_a_sessions_tab_is_let_go_by_its_host(string name)
   {
      _project = SandboxProject.Named(name);
      _window = Open(_project.Window);
      DoneOnTheUIThread(() => _window.StartANewSession(null));
      _tab = OnTheUIThread(() => _window.Tabs.Single());
      WaitUntil(() => _tab.SessionRunningInIt is not null, "Claude Code ran in the tab's host");
   }

   [XF] public void the_tab_closes() => WaitUntil(() => _window.Tabs.Count == 0, "the tab had closed");

   [XF] public void the_window_tells_of_no_failure()
   {
      WaitUntil(() => _window.Tabs.Count == 0, "the tab had closed");
      OnTheUIThread(() => _window.Failure).Must().BeNull();
   }

   public class because_another_attach_client_took_the_session_over : When_a_sessions_tab_is_let_go_by_its_host
   {
      readonly AttachmentToASessionHost _anotherAttachClient;

      public because_another_attach_client_took_the_session_over() : base(nameof(because_another_attach_client_took_the_session_over)) =>
         _anotherAttachClient = AttachmentToASessionHost.To(_tab.Host.Id, ConsoleSize.Default);

      [XF] public void the_session_runs_on_in_its_host()
      {
         WaitUntil(() => _window.Tabs.Count == 0, "the tab had closed");
         _project.SessionRunningIn(_tab.Host.Id).Must().NotBeNull();
      }

      public override void Dispose()
      {
         _anotherAttachClient.Dispose();
         base.Dispose();
      }
   }

   public class because_its_shell_exited : When_a_sessions_tab_is_let_go_by_its_host
   {
      public because_its_shell_exited() : base(nameof(because_its_shell_exited))
      {
         using var shell = Process.GetProcessById(_tab.Host.ShellProcessId);
         shell.Kill();
      }

      [XF] public void the_host_has_ended() => WaitUntil(() => _project.SessionHost(_tab.Host.Id) is null, "the hub no longer listed the host whose shell exited");
   }

   public override void Dispose()
   {
      base.Dispose();
      _project.Dispose();
   }
}
