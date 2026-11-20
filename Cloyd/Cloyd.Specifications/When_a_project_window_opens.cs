using Compze.Must;
using Compze.xUnitBDD;
using static Cloyd.Specifications.HeadlessCloydApp;

namespace Cloyd.Specifications;

public class When_a_project_window_opens
{
   readonly string _title = OnTheUiThread(() =>
   {
      var window = new ProjectWindow(new DirectoryInfo(@"C:\Dev\ClaudeSymbolLinker"));
      window.Show();
      var title = window.Title!;
      window.Close();
      return title;
   });

   [XF] public void it_is_titled_with_the_projects_name() => _title.Must().Be("Cloyd: ClaudeSymbolLinker");
}
