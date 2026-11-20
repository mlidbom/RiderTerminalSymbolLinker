using System.Text.RegularExpressions;
using ClaudeTooling.SessionHost.Protocol;
using Compze.Must;
using Compze.xUnitBDD;
using Cloyd.Hub;
using Cloyd.Projects;
using Cloyd.Saving;
using Cloyd.Sessions;
using Cloyd.Specifications.TheSandbox;

namespace Cloyd.Specifications;

public class When_a_new_session_is_started_in_a_project_window : SpecificationOpeningWindows
{
   const string NameGiven = "Fixing the login bug";

   readonly SandboxProject _project;
   readonly ProjectWindow _window;
   readonly SessionTab _tab;
   readonly RunningSession _session;

   public When_a_new_session_is_started_in_a_project_window() => (_project, _window, _tab, _session) = Once(() =>
   {
      var project = EndWithTheScenario(SandboxProject.Named(nameof(When_a_new_session_is_started_in_a_project_window)));
      var window = Open(project.Window);
      DoneOnTheUIThread(() => window.StartANewSession(NameGiven));
      var tab = OnTheUIThread(() => window.Tabs.Single());
      WaitUntil(() => tab.SessionRunningInIt is not null, "the hub told the tab of the Claude Code in its host");
      var session = OnTheUIThread(() => tab.SessionRunningInIt!);
      WaitUntil(() => tab.Terminal.TextShown.Contains($"New session {session.SessionId}"), "the tab's terminal showed the stand-in's start");
      Render(window, "A-new-session-started");
      return (project, window, tab, session);
   });

   [XF] public void the_window_shows_it_in_a_tab_of_its_own_which_is_selected() => OnTheUIThread(() => _window.SelectedTab).Must().ReferenceEqual(_tab);

   [XF] public void it_starts_one_session_host_for_it() => _project.ItsSessionHosts().Must().HaveCount(1);

   [XF] public void its_session_host_is_labelled_as_one_of_the_projects() => _project.SessionHost(_tab.Host.Id)!.Project.Must().Be(_project.Project);

   [XF] public void its_shell_started_in_the_projects_folder() => _project.SessionHost(_tab.Host.Id)!.Directory.Must().Be(_project.Project.Folder);

   [XF] public void the_tab_is_attached_to_its_host() => _project.SessionHost(_tab.Host.Id)!.Attached.Must().BeTrue();

   [XF] public void the_hub_lists_claude_code_running_in_its_host() => _project.SessionRunningIn(_tab.Host.Id).Must().NotBeNull();

   [XF] public void claude_code_runs_the_session_named_as_Magnus_named_it() => _session.Name.Must().Be(NameGiven);

   [XF] public void the_tab_is_titled_with_the_sessions_name() => OnTheUIThread(() => _tab.Title).Must().Be(NameGiven);

   [XF] public void the_tab_tells_nothing_above_its_terminal() => OnTheUIThread(() => _tab.Notice).Must().BeNull();

   [XF] public void the_window_saves_its_tab_with_the_session_while_it_stays_open()
   {
      WaitUntil(() => _project.SavedWindow.Tabs.SingleOrDefault()?.Session is not null, "the window saved its tab with the session");
      _project.SavedWindow.Tabs.Single().Must().Be(new SavedSessionTab(_tab.Host.Id, _project.Project.Folder, _session.Session));
   }

   [XF] public void the_window_saves_the_tab_as_selected()
   {
      WaitUntil(() => _project.SavedWindow.Tabs.Count == 1, "the window saved its tab");
      _project.SavedWindow.SelectedTab.Must().Be(_tab.Host.Id);
   }

   [XF] public void the_hosts_console_is_the_size_of_the_tabs_terminal()
   {
      ConsoleSize? sizeSaidLast = null;
      WaitUntil(() =>
      {
         sizeSaidLast = SizesSaid(_tab.Terminal.TextShown).LastOrDefault();
         return sizeSaidLast == _tab.Terminal.Size;
      }, "the stand-in said it ran at the terminal's size");
      sizeSaidLast.Must().Be(OnTheUIThread(() => _tab.Terminal.Size));
   }

   /// <summary>The sizes the stand-in said its terminal was, as it started and at each resize: "The terminal is 120 columns by 30 rows".</summary>
   static IEnumerable<ConsoleSize> SizesSaid(string shown) =>
      Regex.Matches(shown, @"The terminal is (\d+) columns by (\d+) rows").Select(it => new ConsoleSize(short.Parse(it.Groups[1].Value), short.Parse(it.Groups[2].Value)));
}
