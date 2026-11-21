using ClaudeTooling.SessionHost.Protocol;
using Compze.Must;
using Compze.xUnitBDD;
using Cloyd.Projects;
using Cloyd.Sessions;
using Cloyd.Specifications.TheSandbox;

namespace Cloyd.Specifications;

/// <summary>A project window with three sessions closes, the second one selected, and their hosts run on.</summary>
public abstract class When_a_project_window_with_several_tabs_closes : SpecificationOpeningWindows
{
   readonly SandboxProject _project;
   readonly SessionHostId[] _hostsInTheirOrder;

   protected When_a_project_window_with_several_tabs_closes() => (_project, _hostsInTheirOrder) = Once(() =>
   {
      var project = Sandbox.Project("the-project");
      var window = Open(project.Window);
      WaitUntil(() => window.BringingBackTheTabs.IsCompleted, "the window had brought back its tabs");
      foreach(var name in new[] { "First", "Second", "Third" }) DoneOnTheUIThread(() => window.StartANewSession(new NewSession(AgentCli.ClaudeCode, name)));
      var hostsInTheirOrder = OnTheUIThread(() => window.Tabs.Select(it => it.Host.Id).ToArray());
      OnTheUIThread(() => window.Tabs[1].IsSelected = true);
      Close(window);
      WaitUntil(() => hostsInTheirOrder.All(host => project.SessionHost(host) is { Attached: false }), "the hub listed the hosts as shown by no attach client");
      return (project, hostsInTheirOrder);
   });

   ProjectWindow OpenedAgain()
   {
      var window = Open(_project.Window);
      WaitUntil(() => window.BringingBackTheTabs.IsCompleted, "the window had brought back its tabs");
      return window;
   }

   public class and_the_project_window_opens_again : When_a_project_window_with_several_tabs_closes
   {
      readonly ProjectWindow _windowAgain;

      public and_the_project_window_opens_again() => _windowAgain = Once(OpenedAgain);

      [XF] public void it_shows_the_hosts_again_in_their_order() => OnTheUIThread(() => _windowAgain.Tabs.Select(it => it.Host.Id).ToArray()).Must().SequenceEqual(_hostsInTheirOrder);

      [XF] public void it_selects_the_tab_that_was_selected() => OnTheUIThread(() => _windowAgain.SelectedTab?.Host.Id).Must().Be(_hostsInTheirOrder[1]);

      [XF] public void it_starts_no_host() => _project.ItsSessionHosts().Must().HaveCount(3);
   }

   public class and_another_host_of_the_project_starts_before_the_window_opens_again : When_a_project_window_with_several_tabs_closes
   {
      readonly SessionHostId _anotherHost;
      readonly ProjectWindow _windowAgain;

      public and_another_host_of_the_project_starts_before_the_window_opens_again() => (_anotherHost, _windowAgain) = Once(() =>
      {
         var anotherHost = _project.StartASessionHost(_project.LabelOfAClaudeSessionsTab).Id;
         return (anotherHost, OpenedAgain());
      });

      [XF] public void it_shows_the_other_host_after_the_saved_tabs() =>
         OnTheUIThread(() => _windowAgain.Tabs.Select(it => it.Host.Id).ToArray()).Must().SequenceEqual([.. _hostsInTheirOrder, _anotherHost]);
   }
}
