using Compze.Must;
using Compze.xUnitBDD;
using Cloyd.Projects;
using Cloyd.Specifications.TheSandbox;

namespace Cloyd.Specifications;

public class When_a_project_window_opens : SpecificationOpeningWindows
{
   readonly SandboxProject _project = SandboxProject.Named("ClaudeSymbolLinker");
   readonly ProjectWindow _window;

   public When_a_project_window_opens()
   {
      _window = Open(_project.Window);
      WaitUntil(() => _window.BringingBackTheTabs.IsCompleted, "the window had brought back its tabs");
   }

   [XF] public void it_is_titled_with_the_projects_name() => OnTheUIThread(() => _window.Title).Must().Be("Cloyd: ClaudeSymbolLinker");

   [XF] public void it_shows_no_tab_when_none_of_the_projects_session_hosts_runs() => OnTheUIThread(() => _window.Tabs).Must().BeEmpty();

   [XF] public void it_tells_of_no_failure() => OnTheUIThread(() => _window.Failure).Must().BeNull();

   public override void Dispose()
   {
      base.Dispose();
      _project.Dispose();
   }
}
