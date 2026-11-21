using Avalonia.Input;
using Cloyd.ClaudeCode;
using Cloyd.History;
using Cloyd.Projects;
using Cloyd.Sessions;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.History;

/// <summary>
/// Magnus clicks a project window's History button. The project is a git repository with a second worktree, and Claude
/// Code has transcripts of sessions in the project's folder, in a folder in it, in the other worktree, and in another
/// project; of one a tool ran with <c>claude -p</c>; and of one with no conversation to resume. None of them runs.
/// Drawn to <c>RenderedWindows/the-session-history-picker.png</c>.
/// </summary>
public class When_Magnus_opens_the_session_history_picker : SpecificationOpeningWindows
{
   private protected readonly SandboxProject _project;
   private protected readonly ProjectWindow _window;
   private protected readonly ClaudeSessionHistoryPicker _picker;

   private protected readonly SessionOfASpecification _titled;
   readonly SessionOfASpecification _inAFolderOfTheProject;
   readonly SessionOfASpecification _inTheOtherWorktree;
   readonly SessionOfASpecification _inAnotherProject;

   public When_Magnus_opens_the_session_history_picker() =>
      (_project, _window, _picker, _titled, _inAFolderOfTheProject, _inTheOtherWorktree, _inAnotherProject) = Once(() =>
      {
         var project = Sandbox.Project("the-project");
         var otherWorktree = new DirectoryInfo(project.Project.Folder + "-other-worktree");
         GitRepositoryOfASpecification.In(new DirectoryInfo(project.Project.Folder)).HasAWorktreeIn(otherWorktree, "other-work");
         var anotherProject = Path.Combine(Sandbox.ProjectsFolder, "another-project");
         var titled = new SessionOfASpecification(new ClaudeCodeSessionId("8f1c0001-6c1e-4a51-9d0b-3a1f00000000"), project.Project.Folder, "Fix the login bug") { Title = "Fixing the login", LastUsedAgo = TimeSpan.FromHours(1) };
         var inAFolderOfTheProject = new SessionOfASpecification(new ClaudeCodeSessionId("8f1c0002-6c1e-4a51-9d0b-3a1f00000000"), Path.Combine(project.Project.Folder, "build"), "Tidy the build script") { LastUsedAgo = TimeSpan.FromHours(2) };
         var inTheOtherWorktree = new SessionOfASpecification(new ClaudeCodeSessionId("8f1c0003-6c1e-4a51-9d0b-3a1f00000000"), otherWorktree.FullName, "Work on the other branch") { Title = "The other worktree's work", Branch = "other-work", LastUsedAgo = TimeSpan.FromMinutes(30) };
         var inAnotherProject = new SessionOfASpecification(new ClaudeCodeSessionId("8f1c0004-6c1e-4a51-9d0b-3a1f00000000"), anotherProject, "Start the other project") { Title = "Another project's work", LastUsedAgo = TimeSpan.FromMinutes(20) };
         var runWithClaudeP = new SessionOfASpecification(new ClaudeCodeSessionId("8f1c0005-6c1e-4a51-9d0b-3a1f00000000"), project.Project.Folder, "Reply with ok") { RunWithClaudeP = true, LastUsedAgo = TimeSpan.FromMinutes(10) };
         var ofNoConversation = new SessionOfASpecification(new ClaudeCodeSessionId("8f1c0006-6c1e-4a51-9d0b-3a1f00000000"), project.Project.Folder, "") { Title = "[cancelled]", HasAConversation = false, LastUsedAgo = TimeSpan.FromMinutes(5) };
         foreach(var session in new[] { titled, inAFolderOfTheProject, inTheOtherWorktree, inAnotherProject, runWithClaudeP, ofNoConversation }) session.WriteItsTranscriptFor(Sandbox.HubOfTheSandbox);

         var window = Open(project.Window);
         WaitUntil(() => window.BringingBackTheTabs.IsCompleted, "the window had brought back its tabs");
         var picker = OnTheUIThread(() => window.HistoryPicker);
         Click(TheButtonWithTheTip(window, ProjectWindow.TipOfTheHistoryButton));
         WaitUntil(() => picker.Scopes.Count > 0, "the picker showed the history");
         Render(window, "the-session-history-picker");
         return (project, window, picker, titled, inAFolderOfTheProject, inTheOtherWorktree, inAnotherProject);
      });

   private protected IReadOnlyList<ClaudeCodeSessionId> Listed => OnTheUIThread(() => _picker.Listed.Select(it => it.Id).ToList());

   [XF] public void it_lists_the_sessions_of_the_projects_worktree_most_recently_used_first_not_those_a_tool_ran_nor_those_of_no_conversation() =>
      Listed.Must().Satisfy(it => it.SequenceEqual([_titled.Id, _inAFolderOfTheProject.Id]));

   [XF] public void each_is_called_by_its_title_else_its_last_prompt() =>
      OnTheUIThread(() => _picker.Listed.Select(it => it.Title).ToList()).Must().Satisfy(it => it.SequenceEqual(["Fixing the login", "Tidy the build script"]));

   [XF] public void it_offers_this_worktree_all_worktrees_and_all_projects() =>
      OnTheUIThread(() => _picker.Scopes).Must().Satisfy(it => it.SequenceEqual(ClaudeSessionHistoryScope.All));

   [XF] public void the_search_box_has_the_keyboard_focus() => OnTheUIThread(() => _picker.TheSearchBoxHasTheFocus).Must().BeTrue();

   [XF] public void the_most_recently_used_is_selected() => OnTheUIThread(() => _picker.Selected).Must().Be(0);

   public class and_widens_it_to_all_worktrees : When_Magnus_opens_the_session_history_picker
   {
      public and_widens_it_to_all_worktrees() => Once(() => OnTheUIThread(() => _picker.List(ClaudeSessionHistoryScope.AllWorktrees)));

      [XF] public void it_lists_the_other_worktrees_session_too() => Listed.Must().Satisfy(it => it.SequenceEqual([_inTheOtherWorktree.Id, _titled.Id, _inAFolderOfTheProject.Id]));

      public class and_then_to_all_projects : and_widens_it_to_all_worktrees
      {
         public and_then_to_all_projects() => Once(() =>
         {
            OnTheUIThread(() => _picker.List(ClaudeSessionHistoryScope.AllProjects));
            Render(_window, "the-session-history-picker-of-all-projects");
         });

         [XF] public void it_lists_the_other_projects_session_too() =>
            Listed.Must().Satisfy(it => it.SequenceEqual([_inAnotherProject.Id, _inTheOtherWorktree.Id, _titled.Id, _inAFolderOfTheProject.Id]));
      }
   }

   public class and_types_words_of_a_sessions_prompt_in_any_case : When_Magnus_opens_the_session_history_picker
   {
      public and_types_words_of_a_sessions_prompt_in_any_case() => Once(() => Type(_window, "LOGIN fix"));

      [XF] public void it_lists_only_that_session() => Listed.Must().Satisfy(it => it.SequenceEqual([_titled.Id]));
   }

   public class and_presses_Down : When_Magnus_opens_the_session_history_picker
   {
      public and_presses_Down() => Once(() => Press(_window, Key.Down, PhysicalKey.ArrowDown, RawInputModifiers.None));

      [XF] public void the_next_session_is_selected() => OnTheUIThread(() => _picker.Selected).Must().Be(1);
   }

   public class and_presses_Escape : When_Magnus_opens_the_session_history_picker
   {
      public and_presses_Escape() => Once(() => Press(_window, Key.Escape, PhysicalKey.Escape, RawInputModifiers.None));

      [XF] public void the_picker_closes() => OnTheUIThread(() => _picker.IsOpen).Must().BeFalse();

      [XF] public void it_resumes_nothing() => OnTheUIThread(() => _window.Tabs.Count).Must().Be(0);
   }

   public class and_presses_Enter : When_Magnus_opens_the_session_history_picker
   {
      readonly SessionTab _tab;

      public and_presses_Enter() => _tab = Once(() =>
      {
         Press(_window, Key.Enter, PhysicalKey.Enter, RawInputModifiers.None);
         WaitUntil(() => _window.Tabs.SingleOrDefault()?.SessionRunningInIt?.SessionId == _titled.Id, "the session was resumed in a tab");
         var tab = OnTheUIThread(() => _window.Tabs.Single());
         Render(_window, "a-session-resumed-from-the-history-picker");
         return tab;
      });

      [XF] public void the_picker_closes() => OnTheUIThread(() => _picker.IsOpen).Must().BeFalse();

      [XF] public void it_resumes_the_session_in_a_new_tab_which_is_selected() => OnTheUIThread(() => _window.SelectedTab).Must().ReferenceEqual(_tab);

      [XF] public void in_the_projects_folder() => _project.SessionHost(_tab.Host.Id)!.Directory.Must().Be(_project.Project.Folder);

      [XF] public void the_tab_is_titled_with_the_sessions_title() => OnTheUIThread(() => _tab.Title).Must().Be("Fixing the login");
   }
}
