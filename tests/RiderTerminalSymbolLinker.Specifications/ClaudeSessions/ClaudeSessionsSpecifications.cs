using Compze.Must;
using Compze.xUnitBDD;
using Xunit;

// ReSharper disable InconsistentNaming

namespace RiderTerminalSymbolLinker.Specifications.ClaudeSessions;

// Each story runs once, in its class fixture, and its specifications check what it saw: a restart takes half a minute.

public class When_a_new_session_starts_in_the_view(When_a_new_session_starts_in_the_view.Story story) : IClassFixture<When_a_new_session_starts_in_the_view.Story>
{
   public sealed class Story
   {
      public readonly TheClaudeSessionsView View = new();
      public readonly SessionTab Tab;
      public readonly IReadOnlyList<string> Saved;

      public Story()
      {
         Tab = View.StartANewSession();
         Thread.Sleep(TimeSpan.FromSeconds(3)); // The keeper looks every two seconds.
         Saved = View.SavedSessions;
      }
   }

   [XF] public void its_tab_runs_claude_code_in_the_projects_folder() => Path.GetFullPath(story.Tab.WorkingDirectory!).Must().Be(story.View.ProjectFolder);

   // The session tooling's reload types /exit into Claude Code, and then `claude --resume <id>` into its parent, which must be a shell.
   [XF] public void claude_code_runs_as_a_child_of_the_tabs_shell() => story.Tab.ClaudeCode!.ParentProcessId.Must().Be(story.Tab.ShellProcessId);

   [XF] public void the_session_is_saved() => story.Saved.Must().Satisfy(it => it.SequenceEqual([story.Tab.SessionId]));
}

public class When_the_user_clears_the_session_in_a_tab(When_the_user_clears_the_session_in_a_tab.Story story) : IClassFixture<When_the_user_clears_the_session_in_a_tab.Story>
{
   public sealed class Story
   {
      public readonly SessionTab Before;
      public readonly SessionTab After;
      public readonly IReadOnlyList<string> Saved;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         Before = view.StartANewSession();
         view.Type(0, "/clear");
         view.WaitUntil(() => view.Tabs[0].SessionId != Before.SessionId, "the tab to run the new session");
         After = view.Tabs[0];
         Thread.Sleep(TimeSpan.FromSeconds(3)); // The keeper looks every two seconds.
         Saved = view.SavedSessions;
      }
   }

   [XF] public void the_tab_runs_the_new_session_in_the_same_claude_code() => story.After.ClaudeCode.Must().Be(story.Before.ClaudeCode);
   [XF] public void the_new_session_is_saved_in_place_of_the_old() => story.Saved.Must().Satisfy(it => it.SequenceEqual([story.After.SessionId]));
}

public class When_claude_code_exits_in_a_tab(When_claude_code_exits_in_a_tab.Story story) : IClassFixture<When_claude_code_exits_in_a_tab.Story>
{
   public sealed class Story
   {
      public readonly SessionTab Before;
      public readonly IReadOnlyList<SessionTab> TabsAfter;
      public readonly IReadOnlyList<string> SavedAfter;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         Before = view.StartANewSession();
         view.ExitClaudeCode(0);
         Thread.Sleep(TimeSpan.FromSeconds(3)); // The keeper looks every two seconds.
         TabsAfter = view.Tabs;
         SavedAfter = view.SavedSessions;
      }
   }

   [XF] public void its_tab_stays_open_on_its_shell() => story.TabsAfter.Must().Satisfy(it => it.Single().ShellProcessId == story.Before.ShellProcessId);
   [XF] public void the_tab_keeps_the_session_that_ran_in_it_last() => story.TabsAfter.Single().SessionId.Must().Be(story.Before.SessionId);
   [XF] public void the_session_stays_saved_to_be_resumed_at_the_next_start() => story.SavedAfter.Must().Satisfy(it => it.SequenceEqual([story.Before.SessionId]));
}

/// <summary>A session reloaded as the session tooling reloads one: <c>/exit</c> typed into Claude Code, and then <c>claude --resume &lt;id&gt;</c> into the shell.</summary>
public class When_a_session_is_reloaded_in_its_tab(When_a_session_is_reloaded_in_its_tab.Story story) : IClassFixture<When_a_session_is_reloaded_in_its_tab.Story>
{
   public sealed class Story
   {
      public readonly SessionTab Before;
      public readonly SessionTab After;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         Before = view.StartANewSession();
         view.ExitClaudeCode(0);
         view.Type(0, $"claude --resume {Before.SessionId}");
         view.WaitUntil(() => view.Tabs[0].ClaudeCode is not null, "Claude Code to run in the tab again");
         After = view.Tabs[0];
      }
   }

   [XF] public void the_tab_runs_the_session_again() => story.After.SessionId.Must().Be(story.Before.SessionId);
   [XF] public void in_a_new_claude_code() => story.After.ClaudeCode!.ProcessId.Must().Satisfy(it => it != story.Before.ClaudeCode!.ProcessId);
   [XF] public void below_the_same_shell() => story.After.ClaudeCode!.ParentProcessId.Must().Be(story.Before.ShellProcessId);
}

public class When_the_IDE_restarts(When_the_IDE_restarts.Story story) : IClassFixture<When_the_IDE_restarts.Story>
{
   public sealed class Story
   {
      public readonly IReadOnlyList<SessionTab> Before;
      public readonly IReadOnlyList<SessionTab> After;
      public readonly IReadOnlyList<string> SecondTabsScreen;
      public readonly bool ShownBefore;
      public readonly bool ShownAfter;
      public readonly int? SelectedAfter;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         for(var tab = 0; tab < 4; tab++)
         {
            view.StartANewSession();
            view.Say(tab, $"a line of session {tab}'s conversation");
         }

         view.ExitClaudeCode(0);
         view.Select(1); // Neither the first tab nor the last, which the view might select whatever was selected.
         view.WaitUntil(() => view.SavedSessions.Count == 4, "the four sessions to be saved");
         Before = view.Tabs;
         ShownBefore = view.IsShown;
         view.DeleteTheTranscriptOf(Before[3].SessionId!);
         view.RestartTheIde(tabsToComeBack: 3);
         After = view.Tabs;
         ShownAfter = view.IsShown;
         SelectedAfter = view.SelectedTab;
         view.WaitUntil(() => view.Screen(1).Any(it => it.StartsWith("> ")), "the second tab to show its conversation");
         SecondTabsScreen = view.Screen(1);
      }
   }

   [XF] public void each_session_that_can_be_resumed_comes_back_in_a_tab_of_its_own_in_the_same_order() =>
      story.After.Select(it => it.SessionId).Must().Satisfy(it => it.SequenceEqual(story.Before.Take(3).Select(before => before.SessionId)));

   [XF] public void a_session_whose_claude_code_had_exited_comes_back_running() => story.After[0].ClaudeCode.Must().Satisfy(it => it is not null);

   [XF] public void a_session_archived_or_deleted_since_does_not_come_back() =>
      story.After.Select(it => it.SessionId).Must().Satisfy(it => !it.Contains(story.Before[3].SessionId));

   [XF] public void the_sessions_are_resumed_in_new_processes() =>
      story.After.Select(it => it.ShellProcessId).Intersect(story.Before.Select(it => it.ShellProcessId)).Must().Satisfy(it => !it.Any());

   [XF] public void a_resumed_session_has_its_conversation() =>
      story.SecondTabsScreen.Must().Contain("> a line of session 1's conversation");

   [XF] public void the_view_is_shown_again_as_it_was_when_the_IDE_closed() => (story.ShownBefore, story.ShownAfter).Must().Be((true, true));
   [XF] public void the_tab_that_was_selected_is_selected_again() => story.SelectedAfter.Must().Be(1);
}

public class When_the_plugin_reloads(When_the_plugin_reloads.Story story) : IClassFixture<When_the_plugin_reloads.Story>
{
   public sealed class Story
   {
      public readonly IReadOnlyList<SessionTab> Before;
      public readonly IReadOnlyList<SessionTab> After;
      public readonly IReadOnlyList<long?> TerminalToolWindowProcessesAfter;
      public readonly int? SelectedAfter;
      public readonly bool ShownBefore;
      public readonly bool ShownAfter;
      public readonly bool TheTerminalToolWindowShownAfter;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         for(var tab = 0; tab < 3; tab++)
         {
            view.StartANewSession();
            view.Say(tab, $"a line of session {tab}'s conversation");
         }

         view.ExitClaudeCode(2);
         view.Select(0); // Not the second, the last tab taken back, which is left selected whatever was.
         Before = view.Tabs;
         ShownBefore = view.IsShown;
         view.ReloadThePlugin();
         view.WaitUntil(() => view.Tabs.Count == 3 && view.Tabs[2].ClaudeCode is not null, "the three tabs to be back in the view, each running Claude Code");
         After = view.Tabs;
         TerminalToolWindowProcessesAfter = view.TerminalToolWindowProcesses;
         SelectedAfter = view.SelectedTab;
         ShownAfter = view.IsShown;
         TheTerminalToolWindowShownAfter = view.TheTerminalToolWindowIsShown;
      }
   }

   [XF] public void the_tabs_that_ran_claude_code_keep_their_processes() => story.After.Take(2).Must().Satisfy(it => it.SequenceEqual(story.Before.Take(2)));

   [XF] public void a_tab_whose_claude_code_had_exited_comes_back_running_its_session_as_after_a_restart() =>
      story.After[2].Must().Satisfy(it => it.SessionId == story.Before[2].SessionId && it.ClaudeCode is not null);

   [XF] public void the_tab_that_was_selected_is_selected_again() => story.SelectedAfter.Must().Be(0);

   // The tabs wait in the Terminal tool window, which shares the bottom of the IDE with the view's: shown, it hides the view.
   [XF] public void the_view_is_shown_as_before_and_not_the_terminal_tool_window() =>
      (story.ShownBefore, story.ShownAfter, story.TheTerminalToolWindowShownAfter).Must().Be((true, true, false));

   [XF] public void none_of_them_is_left_in_the_terminal_tool_window() =>
      story.TerminalToolWindowProcessesAfter.Intersect(story.Before.Concat(story.After).Select(it => it.ShellProcessId)).Must().Satisfy(it => !it.Any());
}
