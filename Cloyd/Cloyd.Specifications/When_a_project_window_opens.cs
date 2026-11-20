using Compze.Must;
using Compze.xUnitBDD;
using Cloyd.Projects;

namespace Cloyd.Specifications;

public class When_a_project_window_opens : SpecificationOpeningWindows
{
   readonly ProjectWindow _window;

   public When_a_project_window_opens()
   {
      var project = Sandbox.Project("ClaudeSymbolLinker");
      _window = Open(project.Window);
      WaitUntil(() => _window.BringingBackTheTabs.IsCompleted, "the window had brought back its tabs");
   }

   [XF] public void it_is_titled_with_the_projects_name() => OnTheUIThread(() => _window.Title).Must().Be("Cloyd: ClaudeSymbolLinker");

   [XF] public void it_shows_no_tab_when_none_of_the_projects_session_hosts_runs() => OnTheUIThread(() => _window.Tabs).Must().BeEmpty();

   [XF] public void it_tells_of_no_failure() => OnTheUIThread(() => _window.Failure).Must().BeNull();
}
