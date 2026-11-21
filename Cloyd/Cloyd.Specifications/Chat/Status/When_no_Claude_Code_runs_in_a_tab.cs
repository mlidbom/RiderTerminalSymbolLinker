using Cloyd.Chat.Status;
using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Status;

/// <summary>A tab whose shell runs no Claude Code, as after Claude Code exits to it: the line under the chat's input box tells so.</summary>
public class When_no_Claude_Code_runs_in_a_tab : SpecificationOpeningWindows
{
   readonly SessionTab _tab;

   public When_no_Claude_Code_runs_in_a_tab()
   {
      var project = Sandbox.Project("the-project");
      project.StartASessionHost(project.LabelOfCloydsHosts);
      var window = Open(project.Window);
      WaitUntil(() => window.BringingBackTheTabs.IsCompleted, "the window had brought back its tabs");
      _tab = OnTheUIThread(() => window.Tabs.Single());
   }

   [XF] public void the_chats_status_line_tells_so() => OnTheUIThread(() => _tab.Chat.StatusLine.ClaudeCodesStatusLine).Must().Be(StatusLineOfTheChat.NoClaudeCodeRuns);
}
