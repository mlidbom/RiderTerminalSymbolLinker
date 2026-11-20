using ClaudeTooling.SessionHost.Protocol;
using Compze.Must;
using Compze.xUnitBDD;
using Cloyd.ClaudeCode;
using Cloyd.Projects;
using Cloyd.Sessions;
using Cloyd.Specifications.TheSandbox;

namespace Cloyd.Specifications;

/// <summary>
/// As after a reboot: a project window with three tabs closes, the second one selected, and every host of the project
/// ends. One tab's host ran no Claude Code, as a shell Magnus worked in.
/// </summary>
public class When_the_session_hosts_of_a_closed_project_window_end : SpecificationOpeningWindows
{
   readonly SandboxProject _project;
   readonly SessionHostId[] _hostsThatEnded;
   readonly ClaudeSession _first, _second;

   public When_the_session_hosts_of_a_closed_project_window_end() => (_project, _hostsThatEnded, _first, _second) = Once(() =>
   {
      var project = Sandbox.Project("the-project");
      project.StartASessionHost(project.LabelOfCloydsHosts);
      var window = Open(project.Window);
      WaitUntil(() => window.BringingBackTheTabs.IsCompleted, "the window had brought back its tabs");
      var first = StartedIn(window, "First");
      var second = StartedIn(window, "Second");
      OnTheUIThread(() => window.Tabs.Single(it => it.LatestSession == first).IsSelected = true);
      Close(window);
      SessionHostId[] hostsThatEnded = [.. project.ItsSessionHosts().Select(it => it.Id)];
      foreach(var host in hostsThatEnded) project.End(host);
      return (project, hostsThatEnded, first, second);
   });

   ClaudeSession StartedIn(ProjectWindow window, string name)
   {
      DoneOnTheUIThread(() => window.StartANewSession(name));
      var tab = OnTheUIThread(() => window.Tabs.Last());
      WaitUntil(() => tab.SessionRunningInIt is not null, $"Claude Code ran the session {name} in its tab's host");
      return OnTheUIThread(() => tab.LatestSession!);
   }

   [XF] public void the_window_saved_all_three_tabs() => _project.SavedWindow.Tabs.Must().HaveCount(3);

   [XF] public void the_window_saved_the_selected_tab() =>
      _project.SavedWindow.Must().Satisfy(it => it.Tabs.Single(tab => tab.SessionHost == it.SelectedTab).Session == _first);

   public class and_the_project_window_opens_again : When_the_session_hosts_of_a_closed_project_window_end
   {
      readonly ProjectWindow _windowAgain;
      readonly IReadOnlyList<SessionTab> _tabs;

      public and_the_project_window_opens_again() => (_windowAgain, _tabs) = Once(() =>
      {
         var windowAgain = Open(_project.Window);
         WaitUntil(() => windowAgain.BringingBackTheTabs.IsCompleted, "the window had brought back its tabs");
         var tabs = OnTheUIThread(() => windowAgain.Tabs);
         WaitUntil(() => tabs.All(it => it.SessionRunningInIt is not null), "the hub told each tab of the Claude Code in its host");
         Render(windowAgain, "Sessions-resumed-after-their-hosts-ended");
         return (windowAgain, tabs);
      });

      [XF] public void it_brings_back_the_tabs_whose_sessions_ran_in_their_order() => OnTheUIThread(() => _tabs.Select(it => it.Title).ToArray()).Must().SequenceEqual(["First", "Second"]);

      [XF] public void it_leaves_out_the_tab_whose_host_ran_no_claude_code() => _tabs.Must().HaveCount(2);

      [XF] public void each_tab_has_a_new_host() => _tabs.Must().Satisfy(it => it.All(tab => !_hostsThatEnded.Contains(tab.Host.Id)));

      [XF] public void each_new_host_is_labelled_as_one_of_the_projects() =>
         _tabs.Must().Satisfy(it => it.All(tab => _project.Project.Equals(_project.SessionHost(tab.Host.Id)!.Project)));

      [XF] public void each_new_host_started_in_the_projects_folder() =>
         _tabs.Must().Satisfy(it => it.All(tab => _project.SessionHost(tab.Host.Id)!.Directory == _project.Project.Folder));

      [XF] public void each_tab_resumed_its_session() =>
         _tabs.Select(it => _project.SessionRunningIn(it.Host.Id)!.SessionId).Must().SequenceEqual([_first.Id, _second.Id]);

      [XF] public void it_selects_the_tab_that_was_selected() => OnTheUIThread(() => _windowAgain.SelectedTab?.LatestSession).Must().Be(_first);

      [XF] public void it_tells_of_no_failure() => OnTheUIThread(() => _windowAgain.Failure).Must().BeNull();

      /// <summary>
      /// Magnus selects each tab in turn, to see its session resumed: a class of its own, since selecting them changes which
      /// tab is selected, which a fact of the window opening again looks at.
      /// </summary>
      public class and_Magnus_selects_each_tab_in_turn : and_the_project_window_opens_again
      {
         readonly IReadOnlyList<(ClaudeSessionId Session, string Shown)> _terminals;

         public and_Magnus_selects_each_tab_in_turn() => _terminals = Once(() => _tabs.Select(ShownOnceSelected).ToList());

         (ClaudeSessionId Session, string Shown) ShownOnceSelected(SessionTab tab)
         {
            OnTheUIThread(() => tab.IsSelected = true); // A terminal is laid out, and shows what it draws, only in the selected tab.
            WaitUntil(() => tab.Terminal.TextShown.Contains($"Resumed session {tab.LatestSession!.Id}"), "the tab's terminal showed the stand-in resume its session");
            return OnTheUIThread(() => (tab.LatestSession!.Id, tab.Terminal.TextShown));
         }

         [XF] public void each_terminal_shows_its_session_resumed() =>
            _terminals.Must().Satisfy(it => it.All(terminal => terminal.Shown.Contains($"Resumed session {terminal.Session}")));
      }
   }

   public class and_the_transcript_of_the_selected_tabs_session_is_gone_as_the_window_opens_again : When_the_session_hosts_of_a_closed_project_window_end
   {
      readonly ProjectWindow _windowAgain;

      public and_the_transcript_of_the_selected_tabs_session_is_gone_as_the_window_opens_again() => _windowAgain = Once(() =>
      {
         Sandbox.HubOfTheSandbox.DeleteTheTranscriptOf(_first.Id);
         var windowAgain = Open(_project.Window);
         WaitUntil(() => windowAgain.BringingBackTheTabs.IsCompleted, "the window had brought back its tabs");
         return windowAgain;
      });

      [XF] public void it_brings_back_only_the_session_it_can_resume() => OnTheUIThread(() => _windowAgain.Tabs.Select(it => it.LatestSession).ToArray()).Must().SequenceEqual([_second]);

      [XF] public void it_selects_that_tab() => OnTheUIThread(() => _windowAgain.SelectedTab?.LatestSession).Must().Be(_second);

      [XF] public void it_tells_of_no_failure() => OnTheUIThread(() => _windowAgain.Failure).Must().BeNull();
   }
}
