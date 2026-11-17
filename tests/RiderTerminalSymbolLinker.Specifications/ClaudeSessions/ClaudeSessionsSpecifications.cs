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

   [XF] public void the_tab_runs_the_new_session_in_the_same_process() => story.After.ProcessId.Must().Be(story.Before.ProcessId);
   [XF] public void the_new_session_is_saved_in_place_of_the_old() => story.Saved.Must().Satisfy(it => it.SequenceEqual([story.After.SessionId]));
}

public class When_the_IDE_restarts(When_the_IDE_restarts.Story story) : IClassFixture<When_the_IDE_restarts.Story>
{
   public sealed class Story
   {
      public readonly IReadOnlyList<SessionTab> Before;
      public readonly IReadOnlyList<SessionTab> After;
      public readonly IReadOnlyList<string> SecondTabsScreen;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         view.StartANewSession();
         view.Type(1, "a line of the second session's conversation");
         view.WaitUntil(() => view.SavedSessions.Count == 2, "both sessions to be saved");
         Before = view.Tabs;
         view.RestartTheIde(tabsToComeBack: 2);
         After = view.Tabs;
         view.WaitUntil(() => view.Screen(1).Any(it => it.StartsWith("> ")), "the second tab to show its conversation");
         SecondTabsScreen = view.Screen(1);
      }
   }

   [XF] public void each_session_comes_back_in_a_tab_of_its_own_in_the_same_order() =>
      story.After.Select(it => it.SessionId).Must().Satisfy(it => it.SequenceEqual(story.Before.Select(before => before.SessionId)));

   [XF] public void the_sessions_are_resumed_in_new_processes() =>
      story.After.Select(it => it.ProcessId).Intersect(story.Before.Select(it => it.ProcessId)).Must().Satisfy(it => !it.Any());

   [XF] public void a_resumed_session_has_its_conversation() =>
      story.SecondTabsScreen.Must().Contain("> a line of the second session's conversation");
}

public class When_the_plugin_reloads(When_the_plugin_reloads.Story story) : IClassFixture<When_the_plugin_reloads.Story>
{
   public sealed class Story
   {
      public readonly IReadOnlyList<SessionTab> Before;
      public readonly IReadOnlyList<SessionTab> After;
      public readonly IReadOnlyList<long?> TerminalToolWindowProcessesAfter;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         view.StartANewSession();
         Before = view.Tabs;
         view.ReloadThePlugin();
         view.WaitUntil(() => view.Tabs.Count == 2, "both tabs to be back in the view");
         After = view.Tabs;
         TerminalToolWindowProcessesAfter = view.TerminalToolWindowProcesses;
      }
   }

   [XF] public void the_tabs_keep_the_processes_that_run_their_sessions() => story.After.Must().Satisfy(it => it.SequenceEqual(story.Before));
   [XF] public void none_of_them_is_left_in_the_terminal_tool_window() =>
      story.TerminalToolWindowProcessesAfter.Intersect(story.After.Select(it => it.ProcessId)).Must().Satisfy(it => !it.Any());
}

public class When_claude_code_exits_in_a_tab(When_claude_code_exits_in_a_tab.Story story) : IClassFixture<When_claude_code_exits_in_a_tab.Story>
{
   public sealed class Story
   {
      public readonly SessionTab Remaining;
      public readonly IReadOnlyList<SessionTab> TabsAfter;
      public readonly IReadOnlyList<string> SavedAfter;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         Remaining = view.StartANewSession();
         view.Type(0, "/exit");
         view.WaitUntil(() => view.Tabs.Count == 1, "the tab to close");
         TabsAfter = view.Tabs;
         view.WaitUntil(() => view.SavedSessions.Count == 1, "the exited session to be no longer saved");
         SavedAfter = view.SavedSessions;
      }
   }

   [XF] public void its_tab_closes() => story.TabsAfter.Must().Satisfy(it => it.SequenceEqual([story.Remaining]));
   [XF] public void its_session_is_no_longer_saved() => story.SavedAfter.Must().Satisfy(it => it.SequenceEqual([story.Remaining.SessionId]));
}
