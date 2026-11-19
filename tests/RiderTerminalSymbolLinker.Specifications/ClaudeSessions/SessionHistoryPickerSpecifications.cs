using System.Diagnostics;
using Compze.Must;
using Compze.xUnitBDD;
using Xunit;

// ReSharper disable InconsistentNaming

namespace RiderTerminalSymbolLinker.Specifications.ClaudeSessions;

// The session history picker: the Resume a Claude Session action's popup, which lists the sessions Claude Code has
// transcripts of, for the user to resume one. Each story runs once, in its class fixture.

/// <summary>Sessions ran in the project's folder, in a folder below it, and elsewhere; a tool ran one; one had no conversation.</summary>
public class When_the_session_history_picker_opens(When_the_session_history_picker_opens.Story story) : IClassFixture<When_the_session_history_picker_opens.Story>
{
   public sealed class Story
   {
      public readonly string Elsewhere;
      public readonly SessionTranscript Named, InASubfolder, OnlyAPrompt, RanElsewhere, RunByATool, WithNoConversation;
      public readonly HistoryPicker AsItOpens, FilteredBySubfolder, FilteredByTwoWordsInAnotherCase, AllProjects;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         var project = view.ProjectFolder;
         Elsewhere = Path.Combine(Path.GetDirectoryName(project)!, "Elsewhere");
         Named = view.Write(new SessionTranscript(project, TimeSpan.FromHours(1), "the first prompt", "the named session's last prompt") { Name = "the-named-session", Title = "About the first prompt" });
         InASubfolder = view.Write(new SessionTranscript(Path.Combine(project, "Below"), TimeSpan.FromHours(2), "a prompt below the project") { Title = "About the subfolder" });
         OnlyAPrompt = view.Write(new SessionTranscript(project, TimeSpan.FromHours(3), "only a prompt"));
         RanElsewhere = view.Write(new SessionTranscript(Elsewhere, TimeSpan.FromMinutes(30), "a prompt elsewhere") { Name = "the-session-elsewhere", GitBranch = "feature" });
         RunByATool = view.Write(new SessionTranscript(project, TimeSpan.FromMinutes(10), "a tool's prompt") { RunByATool = true });
         WithNoConversation = view.Write(new SessionTranscript(project, TimeSpan.FromMinutes(5)));
         AsItOpens = view.OpenThePicker();
         FilteredBySubfolder = view.FilterThePicker("subfolder");
         FilteredByTwoWordsInAnotherCase = view.FilterThePicker("NAMED Session");
         view.FilterThePicker("");
         AllProjects = view.ListInThePicker("ALL_PROJECTS");
         view.CloseThePicker();
      }
   }

   [XF] public void it_lists_the_sessions_of_the_projects_folder_and_below_most_recently_active_first() =>
      story.AsItOpens.Sessions.Must().Satisfy(it => it.SequenceEqual([story.Named.Id, story.InASubfolder.Id, story.OnlyAPrompt.Id]));

   [XF] public void it_calls_a_session_by_its_name_or_else_its_title_or_else_its_last_prompt() =>
      story.AsItOpens.Entries.Select(it => it.Title).Must().Satisfy(it => it.SequenceEqual(["the-named-session", "About the subfolder", "only a prompt"]));

   [XF] public void it_shows_the_last_prompt_after_a_name() =>
      story.AsItOpens.Entries.Select(it => it.LastPrompt).Must().Satisfy(it => it.SequenceEqual(["the named session's last prompt", "a prompt below the project", null]));

   [XF] public void it_offers_no_scope_of_all_worktrees_for_a_project_outside_git() =>
      story.AsItOpens.Scopes.Must().Satisfy(it => it.SequenceEqual(["THIS_WORKTREE", "ALL_PROJECTS"]));

   [XF] public void the_sessions_are_not_running() => story.AllProjects.Entries.Must().Satisfy(it => it.All(entry => entry is { Where: "not-running", CanBeChosen: true }));

   [XF] public void typing_filters_by_what_it_shows() => story.FilteredBySubfolder.Sessions.Must().Satisfy(it => it.SequenceEqual([story.InASubfolder.Id]));

   [XF] public void typing_filters_by_every_word_typed_in_any_case() => story.FilteredByTwoWordsInAnotherCase.Sessions.Must().Satisfy(it => it.SequenceEqual([story.Named.Id]));

   [XF] public void all_projects_lists_every_session() =>
      story.AllProjects.Sessions.Must().Satisfy(it => it.SequenceEqual([story.RanElsewhere.Id, story.Named.Id, story.InASubfolder.Id, story.OnlyAPrompt.Id]));

   [XF] public void it_leaves_out_the_sessions_a_tool_ran_and_those_with_no_conversation() =>
      story.AllProjects.Sessions.Must().Satisfy(it => !it.Contains(story.RunByATool.Id) && !it.Contains(story.WithNoConversation.Id));

   [XF] public void it_tells_the_folder_each_session_last_ran_in_and_its_branch() =>
      (story.AllProjects.Entries[0].Folder, story.AllProjects.Entries[0].GitBranch).Must().Be((story.Elsewhere, "feature"));
}

/// <summary>The project is a git repository with another worktree, and sessions ran in each worktree, and elsewhere.</summary>
public class When_the_project_has_other_git_worktrees(When_the_project_has_other_git_worktrees.Story story) : IClassFixture<When_the_project_has_other_git_worktrees.Story>
{
   public sealed class Story : IDisposable
   {
      readonly string _project;
      readonly string _otherWorktree;
      public readonly SessionTranscript InThisWorktree, InTheOtherWorktree, Elsewhere;
      public readonly HistoryPicker ThisWorktree, AllWorktrees, AllProjects;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         _project = view.ProjectFolder;
         _otherWorktree = Path.Combine(Path.GetDirectoryName(_project)!, "OtherWorktree");
         Git(_project, "init");
         Git(_project, "-c", "user.name=Sandbox", "-c", "user.email=sandbox@localhost", "commit", "--allow-empty", "-m", "A commit for the other worktree to check out");
         Git(_project, "worktree", "add", _otherWorktree);
         InThisWorktree = view.Write(new SessionTranscript(_project, TimeSpan.FromHours(1), "a prompt in this worktree"));
         InTheOtherWorktree = view.Write(new SessionTranscript(_otherWorktree, TimeSpan.FromHours(2), "a prompt in the other worktree"));
         Elsewhere = view.Write(new SessionTranscript(Path.Combine(Path.GetDirectoryName(_project)!, "Elsewhere"), TimeSpan.FromHours(3), "a prompt elsewhere"));
         ThisWorktree = view.OpenThePicker();
         AllWorktrees = view.ListInThePicker("ALL_WORKTREES");
         AllProjects = view.ListInThePicker("ALL_PROJECTS");
         view.CloseThePicker();
      }

      public void Dispose()
      {
         Git(_project, "worktree", "remove", "--force", _otherWorktree);
         DeleteWithItsReadOnlyFiles(Path.Combine(_project, ".git"));
      }

      static void Git(string folder, params string[] arguments)
      {
         var start = new ProcessStartInfo("git", ["-C", folder, .. arguments]) { RedirectStandardOutput = true, RedirectStandardError = true };
         using var git = Process.Start(start)!;
         var errors = git.StandardError.ReadToEndAsync();
         git.StandardOutput.ReadToEnd();
         git.WaitForExit();
         if(git.ExitCode != 0) throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed in {folder}: {errors.Result}");
      }

      /// <summary>Deletes <paramref name="folder"/>: git makes its objects read-only, which Windows won't delete as they are.</summary>
      static void DeleteWithItsReadOnlyFiles(string folder)
      {
         foreach(var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
         Directory.Delete(folder, recursive: true);
      }
   }

   [XF] public void it_offers_every_scope() => story.ThisWorktree.Scopes.Must().Satisfy(it => it.SequenceEqual(["THIS_WORKTREE", "ALL_WORKTREES", "ALL_PROJECTS"]));

   [XF] public void it_lists_this_worktrees_sessions_as_it_opens() => story.ThisWorktree.Sessions.Must().Satisfy(it => it.SequenceEqual([story.InThisWorktree.Id]));

   [XF] public void all_worktrees_lists_the_sessions_of_every_worktree() =>
      story.AllWorktrees.Sessions.Must().Satisfy(it => it.SequenceEqual([story.InThisWorktree.Id, story.InTheOtherWorktree.Id]));

   [XF] public void all_projects_lists_every_session() =>
      story.AllProjects.Sessions.Must().Satisfy(it => it.SequenceEqual([story.InThisWorktree.Id, story.InTheOtherWorktree.Id, story.Elsewhere.Id]));
}

/// <summary>Three sessions ran in the project: the user picks one with the keyboard, and then one with the mouse.</summary>
public class When_the_user_picks_sessions_with_the_keyboard_and_the_mouse(When_the_user_picks_sessions_with_the_keyboard_and_the_mouse.Story story)
   : IClassFixture<When_the_user_picks_sessions_with_the_keyboard_and_the_mouse.Story>
{
   public sealed class Story
   {
      public readonly SessionTranscript First, Second, Third;
      public readonly int? SelectedAsItOpens, SelectedAfterDown;
      public readonly bool ShownAfterEnter;
      public readonly IReadOnlyList<SessionTab> Tabs;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         First = view.Write(new SessionTranscript(view.ProjectFolder, TimeSpan.FromHours(1), "the first prompt"));
         Second = view.Write(new SessionTranscript(view.ProjectFolder, TimeSpan.FromHours(2), "the second prompt"));
         Third = view.Write(new SessionTranscript(view.ProjectFolder, TimeSpan.FromHours(3), "the third prompt"));
         SelectedAsItOpens = view.OpenThePicker().Selected;
         SelectedAfterDown = view.PressInThePicker("DOWN").Selected;
         ShownAfterEnter = view.PressInThePicker("ENTER").Shown;
         view.WaitUntil(() => view.Tabs is [{ ClaudeCode: not null }], "Enter to resume the selected session in a tab");
         var reopened = view.OpenThePicker();
         view.DoubleClickInThePicker(reopened.Sessions.ToList().IndexOf(Third.Id));
         view.WaitUntil(() => view.Tabs is [_, { ClaudeCode: not null }], "the double click to resume the session in a tab");
         Tabs = view.Tabs;
      }
   }

   [XF] public void the_first_entry_is_selected_as_it_opens() => story.SelectedAsItOpens.Must().Be(0);

   [XF] public void down_selects_the_next() => story.SelectedAfterDown.Must().Be(1);

   [XF] public void enter_closes_the_picker() => story.ShownAfterEnter.Must().BeFalse();

   [XF] public void enter_resumes_the_selected_session() => story.Tabs[0].SessionId.Must().Be(story.Second.Id);

   [XF] public void a_double_click_resumes_the_session_clicked() => story.Tabs[1].SessionId.Must().Be(story.Third.Id);
}

/// <summary>A session ran in another folder, and no Claude Code runs it: the user chooses it.</summary>
public class When_the_user_chooses_a_session_no_claude_code_runs(When_the_user_chooses_a_session_no_claude_code_runs.Story story)
   : IClassFixture<When_the_user_chooses_a_session_no_claude_code_runs.Story>
{
   public sealed class Story
   {
      public readonly string ProjectFolder;
      public readonly SessionTranscript Session;
      public readonly HistoryPicker AfterChoosing;
      public readonly IReadOnlyList<SessionTab> Tabs;
      public readonly TerminalSize? SizeTheClaudeCodeDrawsAt;
      public readonly TerminalSize? SizeOfTheTab;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         ProjectFolder = view.ProjectFolder;
         Session = view.Write(new SessionTranscript(Path.Combine(Path.GetDirectoryName(ProjectFolder)!, "Elsewhere"), TimeSpan.FromMinutes(1), "what was said elsewhere"));
         var picker = view.OpenThePicker();
         picker = view.ListInThePicker("ALL_PROJECTS");
         AfterChoosing = view.ChooseInThePicker(picker.Sessions.ToList().IndexOf(Session.Id));
         view.WaitUntil(() => view.Tabs is [{ ClaudeCode: not null } tab] && tab.SessionId == Session.Id, "a tab to resume the session");
         view.WaitUntil(() => view.Screen(0).Contains("> what was said elsewhere"), "the tab to show the session's conversation");
         Tabs = view.Tabs;
         SizeTheClaudeCodeDrawsAt = view.SizeTheClaudeCodeDrawsAtIn(0);
         SizeOfTheTab = view.SizeOf(0);
      }
   }

   [XF] public void the_picker_closes() => story.AfterChoosing.Shown.Must().BeFalse();

   [XF] public void a_new_tab_resumes_the_session() => story.Tabs.Single().SessionId.Must().Be(story.Session.Id);

   [XF] public void it_runs_in_the_projects_folder_where_a_new_session_starts() => Path.GetFullPath(story.Tabs.Single().WorkingDirectory!).Must().Be(story.ProjectFolder);

   [XF] public void its_claude_code_draws_itself_at_the_size_of_the_tab() => story.SizeTheClaudeCodeDrawsAt.Must().Be(story.SizeOfTheTab);
}

/// <summary>A session ran in a tab, and its Claude Code exited there, leaving the shell at its prompt: the user chooses it.</summary>
public class When_the_user_chooses_a_session_whose_claude_code_exited_in_a_tab(When_the_user_chooses_a_session_whose_claude_code_exited_in_a_tab.Story story)
   : IClassFixture<When_the_user_chooses_a_session_whose_claude_code_exited_in_a_tab.Story>
{
   public sealed class Story
   {
      public readonly SessionTab Before;
      public readonly PickerEntry Entry;
      public readonly IReadOnlyList<SessionTab> After;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         view.Say(0, "a prompt before Claude Code exits");
         view.ExitClaudeCode(0);
         Before = view.Tabs[0];
         var picker = view.OpenThePicker();
         Entry = picker.Entries.Single(it => it.Session == Before.SessionId);
         view.ChooseInThePicker(picker.Sessions.ToList().IndexOf(Before.SessionId!));
         view.WaitUntil(() => view.Tabs[0].ClaudeCode is not null, "Claude Code to run in the tab again");
         After = view.Tabs;
      }
   }

   [XF] public void the_picker_lists_it_as_open_in_a_tab() => story.Entry.Where.Must().Be("exited-in-a-tab");

   [XF] public void it_is_resumed_in_that_tab() => story.After.Must().Satisfy(it => it is [var tab] && tab.ShellProcessId == story.Before.ShellProcessId && tab.SessionId == story.Before.SessionId);
}

/// <summary>Sessions run in two tabs, the second one shown: the user chooses the first tab's session.</summary>
public class When_the_user_chooses_a_session_that_runs_in_a_tab(When_the_user_chooses_a_session_that_runs_in_a_tab.Story story)
   : IClassFixture<When_the_user_chooses_a_session_that_runs_in_a_tab.Story>
{
   public sealed class Story
   {
      public readonly PickerEntry Entry;
      public readonly int? SelectedTab;
      public readonly int TabCount;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         var first = view.StartANewSession();
         view.Say(0, "the first tab's prompt");
         view.StartANewSession();
         view.Say(1, "the second tab's prompt");
         view.Select(1);
         var picker = view.OpenThePicker();
         Entry = picker.Entries.Single(it => it.Session == first.SessionId);
         view.ChooseInThePicker(picker.Sessions.ToList().IndexOf(first.SessionId!));
         view.WaitUntil(() => view.SelectedTab == 0, "the first tab to be shown");
         SelectedTab = view.SelectedTab;
         TabCount = view.Tabs.Count;
      }
   }

   [XF] public void the_picker_lists_it_as_running_in_a_tab() => (story.Entry.Where, story.Entry.CanBeChosen).Must().Be(("running-in-a-tab", true));

   [XF] public void its_tab_is_shown() => story.SelectedTab.Must().Be(0);

   [XF] public void no_tab_is_opened() => story.TabCount.Must().Be(2);
}

/// <summary>A session runs in a Claude Code outside Claude Sessions' tabs, as in a plain terminal: the user tries to choose it.</summary>
public class When_the_user_tries_to_choose_a_session_that_runs_outside_claude_sessions(When_the_user_tries_to_choose_a_session_that_runs_outside_claude_sessions.Story story)
   : IClassFixture<When_the_user_tries_to_choose_a_session_that_runs_outside_claude_sessions.Story>
{
   public sealed class Story : IDisposable
   {
      readonly ClaudeCodeOutsideTheView _claudeCode;
      public readonly PickerEntry Entry;
      public readonly HistoryPicker AfterChoosing;
      public readonly int TabCount;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         var session = view.Write(new SessionTranscript(view.ProjectFolder, TimeSpan.FromMinutes(1), "a prompt in a plain terminal"));
         _claudeCode = view.RunClaudeCodeOutsideTheView(session.Id, view.ProjectFolder);
         var picker = view.OpenThePicker();
         Entry = picker.Entries.Single(it => it.Session == session.Id);
         AfterChoosing = view.ChooseInThePicker(picker.Sessions.ToList().IndexOf(session.Id));
         Thread.Sleep(TimeSpan.FromSeconds(2)); // Time for a tab to open, were one to.
         TabCount = view.Tabs.Count;
         view.CloseThePicker();
      }

      public void Dispose() => _claudeCode.Dispose();
   }

   [XF] public void the_picker_lists_it_as_running_outside_claude_sessions() => story.Entry.Where.Must().Be("running-outside-claude-sessions");

   [XF] public void it_cannot_be_chosen() => story.Entry.CanBeChosen.Must().BeFalse();

   [XF] public void choosing_it_leaves_the_picker_open() => story.AfterChoosing.Shown.Must().BeTrue();

   [XF] public void choosing_it_opens_no_tab() => story.TabCount.Must().Be(0);
}

/// <summary>A session runs in a tab of another IDE's Claude Sessions: the user chooses it.</summary>
public class When_the_user_chooses_a_session_that_runs_in_another_ide(When_the_user_chooses_a_session_that_runs_in_another_ide.Story story)
   : IClassFixture<When_the_user_chooses_a_session_that_runs_in_another_ide.Story>
{
   public sealed class Story : IDisposable
   {
      readonly ClaudeCodeOutsideTheView _claudeCode;
      readonly AnotherIdeStandIn _anotherIde;
      public readonly string Session;
      public readonly long ClaudeCodeProcess;
      public readonly PickerEntry Entry;
      public readonly HistoryPicker AfterChoosing;
      public readonly IReadOnlyList<string> TheOtherIdeWasAskedToShow;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         Session = view.Write(new SessionTranscript(view.ProjectFolder, TimeSpan.FromMinutes(1), "a prompt in another IDE")).Id;
         _claudeCode = view.RunClaudeCodeOutsideTheView(Session, view.ProjectFolder);
         ClaudeCodeProcess = _claudeCode.Process.Id;
         _anotherIde = new AnotherIdeStandIn(view.LocalApiAnnouncementsFolder, (Session, ClaudeCodeProcess));
         var picker = view.OpenThePicker();
         Entry = picker.Entries.Single(it => it.Session == Session);
         AfterChoosing = view.ChooseInThePicker(picker.Sessions.ToList().IndexOf(Session));
         view.WaitUntil(() => _anotherIde.AskedToShow.Count > 0, "the other IDE to be asked to show the session");
         TheOtherIdeWasAskedToShow = _anotherIde.AskedToShow;
      }

      public void Dispose()
      {
         _anotherIde.Dispose();
         _claudeCode.Dispose();
      }
   }

   [XF] public void the_picker_lists_it_as_running_in_another_ide() => (story.Entry.Where, story.Entry.CanBeChosen).Must().Be(("running-in-another-ide", true));

   [XF] public void the_picker_closes() => story.AfterChoosing.Shown.Must().BeFalse();

   [XF] public void the_other_ide_is_asked_to_show_its_tab() =>
      story.TheOtherIdeWasAskedToShow.Must().Satisfy(it => it.SequenceEqual([$"session={story.Session}&process={story.ClaudeCodeProcess}"]));
}
