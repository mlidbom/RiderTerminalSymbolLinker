using Avalonia.Input;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.ClaudeCode;
using Cloyd.History;
using Cloyd.Hub;
using Cloyd.Projects;
using Cloyd.SessionHosts;
using Cloyd.Sessions;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.History;

/// <summary>
/// Magnus opens the session history picker of a project's window, and widens it to all projects, while sessions he gave a
/// prompt run, or ran: two in tabs of the window, the first and the second, which is selected; one in a tab whose Claude
/// Code he exited; one in a session host a console took over from its tab; one in a terminal of its own, outside every
/// host; and one in a tab of another project's window. What choosing each does depends on where it is.
/// Drawn to <c>RenderedWindows/the-session-history-picker-while-sessions-run.png</c>.
/// </summary>
public class When_Magnus_opens_the_session_history_picker_while_sessions_run : SpecificationOpeningWindows
{
   readonly SandboxProject _anotherProject;
   private protected readonly ProjectWindow _window;
   private protected readonly ProjectWindow _anotherProjectsWindow;
   private protected readonly ClaudeSessionHistoryPicker _picker;
   private protected readonly SessionTab _first;
   private protected readonly SessionTab _exited;
   private protected readonly SessionHostId _takenOverByAConsole;
   private protected readonly ClaudeCodeSessionId _firsts, _seconds, _exiteds, _takenOvers, _inATerminalOfItsOwns, _anotherProjects;

   public When_Magnus_opens_the_session_history_picker_while_sessions_run() =>
      (_anotherProject, _window, _anotherProjectsWindow, _picker, _first, _exited, _takenOverByAConsole, _firsts, _seconds, _exiteds, _takenOvers, _inATerminalOfItsOwns, _anotherProjects) = Once(() =>
      {
         var project = Sandbox.Project("the-project");
         var anotherProject = Sandbox.Project("another-project");
         var anotherProjectsWindow = Open(anotherProject.Window);
         var anotherProjects = StartedWithAPrompt(anotherProjectsWindow, "In another project").Session;
         var window = Open(project.Window);
         var (first, firsts) = StartedWithAPrompt(window, "First");
         var (exited, exiteds) = StartedWithAPrompt(window, "Exited");
         DoneOnTheUIThread(() => exited.Terminal.SendInputAsync("/exit\r"));
         WaitUntil(() => exited.SessionRunningInIt is null, "the hub told the tab its Claude Code had exited");
         var (takenOver, takenOvers) = StartedWithAPrompt(window, "Taken over");
         EndWithTheScenario(AttachmentToASessionHost.To(takenOver.Host.Id, ConsoleSize.Default));
         WaitUntil(() => !window.Tabs.Contains(takenOver), "the tab let go of the host the console took over");
         var seconds = StartedWithAPrompt(window, "Second").Session;
         var inATerminalOfItsOwn = EndWithTheScenario(ClaudeCodeInATerminalOfItsOwn.Running(Sandbox.HubOfTheSandbox, project.Project.Folder, "In a terminal of its own", "Hello from a terminal of its own"));
         WaitUntil(() => HasAConversationToResume(Sandbox.HubOfTheSandbox.RunningClaudeCodes().SingleOrDefault(it => it.Process == inATerminalOfItsOwn.Process)?.SessionId), "the hub listed the session in a terminal of its own, with its prompt");
         var inATerminalOfItsOwns = Sandbox.HubOfTheSandbox.RunningClaudeCodes().Single(it => it.Process == inATerminalOfItsOwn.Process).SessionId;

         var picker = OnTheUIThread(() => window.HistoryPicker);
         Press(window, Key.Y, PhysicalKey.Y, RawInputModifiers.Control | RawInputModifiers.Alt | RawInputModifiers.Shift);
         WaitUntil(() => picker.Scopes.Count > 0, "the picker showed the history");
         OnTheUIThread(() => picker.List(ClaudeSessionHistoryScope.AllProjects));
         Render(window, "the-session-history-picker-while-sessions-run");
         return (anotherProject, window, anotherProjectsWindow, picker, first, exited, takenOver.Host.Id, firsts, seconds, exiteds, takenOvers, inATerminalOfItsOwns, anotherProjects);
      });

   /// <summary>
   /// A new session started in a tab of <paramref name="window"/>, named <paramref name="name"/>, which Magnus gave a
   /// prompt, once the hub lists it with its conversation: the tab, and the session.
   /// </summary>
   (SessionTab Tab, ClaudeCodeSessionId Session) StartedWithAPrompt(ProjectWindow window, string name)
   {
      DoneOnTheUIThread(() => window.StartANewSession(new NewSession(AgentCli.ClaudeCode, name)));
      var tab = OnTheUIThread(() => window.Tabs[^1]);
      WaitUntil(() => tab.SessionRunningInIt is not null, $"the hub told the tab of {name} of its Claude Code");
      var session = OnTheUIThread(() => ((RunningClaudeCode)tab.SessionRunningInIt!).SessionId);
      DoneOnTheUIThread(() => tab.Terminal.SendInputAsync($"Hello from {name}\r"));
      WaitUntil(() => HasAConversationToResume(session), $"the hub listed {name} with its prompt");
      return (tab, session);
   }

   bool HasAConversationToResume(ClaudeCodeSessionId? session) =>
      session is {} asked && Sandbox.HubOfTheSandbox.TranscribedSessions().Any(it => it.Id == asked && it.HasAConversationToResume);

   /// <summary>Selects the entry of <paramref name="session"/> in the picker, as a click on it does, and presses Enter.</summary>
   private protected void Choose(ClaudeCodeSessionId session)
   {
      OnTheUIThread(() => _picker.Selected = _picker.Listed.ToList().FindIndex(it => it.Id == session));
      Press(_window, Key.Enter, PhysicalKey.Enter, RawInputModifiers.None);
   }

   private protected IReadOnlyList<SessionTab> Tabs => OnTheUIThread(() => _window.Tabs.ToList());

   [XF] public void it_tells_where_each_session_is() =>
      OnTheUIThread(() => _picker.Listed.ToDictionary(it => it.Id, it => it.Where.AsToldIn(_window)))
        .Must().Satisfy(told => told[_firsts] == "running in a tab"
                             && told[_seconds] == "running in a tab"
                             && told[_exiteds] == "open in a tab"
                             && told[_takenOvers] == "running, shown outside Cloyd"
                             && told[_inATerminalOfItsOwns] == "running outside Cloyd"
                             && told[_anotherProjects] == $"running in a tab of {_anotherProject.Project.Name}");

   public class and_chooses_a_session_that_runs_in_another_tab : When_Magnus_opens_the_session_history_picker_while_sessions_run
   {
      public and_chooses_a_session_that_runs_in_another_tab() => Once(() => Choose(_firsts));

      [XF] public void the_window_selects_its_tab() => OnTheUIThread(() => _window.SelectedTab).Must().ReferenceEqual(_first);

      [XF] public void it_adds_no_tab() => Tabs.Count.Must().Be(3);
   }

   public class and_chooses_a_session_that_runs_in_another_projects_window : When_Magnus_opens_the_session_history_picker_while_sessions_run
   {
      public and_chooses_a_session_that_runs_in_another_projects_window() => Once(() => Choose(_anotherProjects));

      [XF] public void the_other_window_is_brought_forward() => OnTheUIThread(() => _anotherProjectsWindow.IsActive).Must().BeTrue();

      [XF] public void it_adds_no_tab_here() => Tabs.Count.Must().Be(3);
   }

   public class and_chooses_a_session_whose_Claude_Code_exited_in_a_tab : When_Magnus_opens_the_session_history_picker_while_sessions_run
   {
      public and_chooses_a_session_whose_Claude_Code_exited_in_a_tab() => Once(() =>
      {
         Choose(_exiteds);
         WaitUntil(() => _exited.SessionRunningInIt?.Session.Id == _exiteds, "the session was resumed in its tab");
      });

      [XF] public void it_resumes_it_in_that_tab_which_is_selected() => OnTheUIThread(() => _window.SelectedTab).Must().ReferenceEqual(_exited);

      [XF] public void it_adds_no_tab() => Tabs.Count.Must().Be(3);
   }

   public class and_chooses_a_session_whose_host_a_console_took_over : When_Magnus_opens_the_session_history_picker_while_sessions_run
   {
      public and_chooses_a_session_whose_host_a_console_took_over() => Once(() =>
      {
         Choose(_takenOvers);
         WaitUntil(() => _window.Tabs.Any(it => it.Host.Id == _takenOverByAConsole), "the window showed the host in a tab");
      });

      [XF] public void it_shows_the_host_in_a_new_tab_which_is_selected() => OnTheUIThread(() => _window.SelectedTab!.Host.Id).Must().Be(_takenOverByAConsole);

      [XF] public void the_tab_is_titled_with_the_session() => OnTheUIThread(() => _window.SelectedTab!.Title).Must().Be("Taken over");
   }

   public class and_presses_Enter_on_a_session_that_runs_in_a_terminal_of_its_own : When_Magnus_opens_the_session_history_picker_while_sessions_run
   {
      public and_presses_Enter_on_a_session_that_runs_in_a_terminal_of_its_own() => Once(() => Choose(_inATerminalOfItsOwns));

      [XF] public void the_picker_stays_open() => OnTheUIThread(() => _picker.IsOpen).Must().BeTrue();

      [XF] public void it_adds_no_tab() => Tabs.Count.Must().Be(3);
   }
}
