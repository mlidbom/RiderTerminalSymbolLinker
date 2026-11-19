using System.Text.Json.Nodes;
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
      public readonly IReadOnlyList<SavedTab> Saved;
      public readonly IReadOnlyList<string> SessionHosts;

      public Story()
      {
         Tab = View.StartANewSession();
         Thread.Sleep(TimeSpan.FromSeconds(3)); // The keeper looks every two seconds.
         Saved = View.SavedTabs;
         SessionHosts = View.SessionHosts;
      }
   }

   [XF] public void its_tab_runs_claude_code_in_the_projects_folder() => Path.GetFullPath(story.Tab.WorkingDirectory!).Must().Be(story.View.ProjectFolder);

   // The session tooling's reload types /exit into Claude Code, and then `claude --resume <id>` into its parent, which must be a shell.
   [XF] public void claude_code_runs_as_a_child_of_the_tabs_shell() => story.Tab.ClaudeCode!.ParentProcessId.Must().Be(story.Tab.ShellProcessId);

   // Outside the IDE, so that the session outlives it.
   [XF] public void the_shell_runs_in_a_session_host_of_its_own() => story.SessionHosts.Must().Satisfy(it => it.SequenceEqual([story.Tab.SessionHost!]));

   [XF] public void the_session_is_saved_with_its_session_host() => story.Saved.Must().Satisfy(it => it.SequenceEqual([new SavedTab(story.Tab.SessionId, story.Tab.SessionHost)]));
}

public class When_the_user_closes_a_tab(When_the_user_closes_a_tab.Story story) : IClassFixture<When_the_user_closes_a_tab.Story>
{
   public sealed class Story
   {
      public readonly SessionTab Closed;
      public readonly SessionTab Other;
      public readonly IReadOnlyList<SessionTab> TabsAfter;
      public readonly IReadOnlyList<string> SessionHostsAfter;
      public readonly IReadOnlyList<int> RunningClaudeCodesAfter;
      public readonly IReadOnlyList<string> SavedAfter;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         Closed = view.StartANewSession();
         Other = view.StartANewSession();
         view.Close(0);
         view.WaitUntil(() => view.Tabs.Count == 1 && view.SessionHosts.Count == 1, "the tab and its session host to be gone");
         view.WaitUntil(() => !view.RunningClaudeCodes.Contains((int)Closed.ClaudeCode!.ProcessId), "the hub to tell the closed tab's Claude Code has exited");
         Thread.Sleep(TimeSpan.FromSeconds(3)); // The keeper looks every two seconds.
         TabsAfter = view.Tabs;
         SessionHostsAfter = view.SessionHosts;
         RunningClaudeCodesAfter = view.RunningClaudeCodes;
         SavedAfter = view.SavedSessions;
      }
   }

   [XF] public void its_session_ends_with_its_session_host() => story.SessionHostsAfter.Must().Satisfy(it => !it.Contains(story.Closed.SessionHost!));
   [XF] public void its_claude_code_exits() => story.RunningClaudeCodesAfter.Must().Satisfy(it => !it.Contains((int)story.Closed.ClaudeCode!.ProcessId));
   [XF] public void the_other_tabs_session_runs_on() => story.TabsAfter.Must().Satisfy(it => it.SequenceEqual([story.Other]));
   [XF] public void it_is_no_longer_saved() => story.SavedAfter.Must().Satisfy(it => it.SequenceEqual([story.Other.SessionId!]));
}

/// <summary>
///   The process of a tab's terminal, its session host's attach client, ends by itself, as when another terminal attaches
///   to the session and takes it over, or the attach client crashes.
/// </summary>
public class When_a_tabs_attach_client_ends_by_itself(When_a_tabs_attach_client_ends_by_itself.Story story) : IClassFixture<When_a_tabs_attach_client_ends_by_itself.Story>
{
   public sealed class Story
   {
      public readonly SessionTab Before;
      public readonly int TabsAfter;
      public readonly IReadOnlyList<string> SessionHostsAfter;
      public readonly bool AttachedAfter;
      public readonly IReadOnlyList<int> RunningClaudeCodesAfter;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         Before = view.StartANewSession();
         view.WaitUntil(() => view.AttachClients[0] is not null, "the tab's attach client to run");
         view.EndTheAttachClientOf(0);
         view.WaitUntil(() => view.Tabs.Count == 0, "the tab to close");
         TabsAfter = view.Tabs.Count;
         SessionHostsAfter = view.SessionHosts;
         AttachedAfter = view.IsAttached(Before.SessionHost!);
         RunningClaudeCodesAfter = view.RunningClaudeCodes;
      }
   }

   [XF] public void the_tab_closes() => story.TabsAfter.Must().Be(0);
   [XF] public void its_session_runs_on_in_its_session_host() => story.SessionHostsAfter.Must().Contain(story.Before.SessionHost!);
   [XF] public void which_no_attach_client_shows() => story.AttachedAfter.Must().BeFalse();
   [XF] public void its_claude_code_included() => story.RunningClaudeCodesAfter.Must().Contain((int)story.Before.ClaudeCode!.ProcessId);
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

/// <summary>A terminal tab that the view didn't open comes into its tool window, and Claude Code is started in it by hand.</summary>
public class When_a_tab_the_view_did_not_open_is_in_its_tool_window(When_a_tab_the_view_did_not_open_is_in_its_tool_window.Story story)
   : IClassFixture<When_a_tab_the_view_did_not_open_is_in_its_tool_window.Story>
{
   public sealed class Story
   {
      public readonly int PlainTab;
      public readonly IReadOnlyList<SessionTab> Before;
      public readonly IReadOnlyList<string> SavedBefore;
      public readonly IReadOnlyList<SessionTab> After;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         PlainTab = view.OpenAPlainTerminalTab();
         view.Type(PlainTab, "claude");
         view.WaitUntil(() => view.Tabs[PlainTab].SessionId is not null, "the view to find the session Claude Code runs in the plain tab");
         view.Say(PlainTab, "a line of the plain tab's conversation"); // Claude Code keeps no transcript of a session without one, and can't resume it.
         view.WaitUntil(() => view.SavedSessions.Count == 2, "both sessions to be saved");
         SavedBefore = view.SavedSessions;
         Before = view.Tabs;
         view.ReloadThePlugin();
         view.WaitUntil(() => view.Tabs.Count == 2 && view.Tabs.All(it => it.ClaudeCode is not null), "both tabs to be back in the view, running their sessions");
         After = view.Tabs;
      }
   }

   [XF] public void the_view_takes_it_as_one_of_its_tabs() => story.PlainTab.Must().Be(1);
   [XF] public void its_session_is_saved() => story.SavedBefore.Must().Contain(story.Before[1].SessionId!);

   // Its shell ran in the tab's terminal, which went with the plugin's tool window: not in a session host, which outlives it.
   [XF] public void after_a_reload_of_the_plugin_its_session_comes_back_resumed_in_a_session_host() =>
      story.After[1].Must().Satisfy(it => it.SessionId == story.Before[1].SessionId && it.SessionHost is not null && it.ClaudeCode!.ProcessId != story.Before[1].ClaudeCode!.ProcessId);

   [XF] public void and_the_tab_the_view_opened_comes_back_as_it_was() => story.After[0].Must().Be(story.Before[0]);
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

/// <summary>A tool asks the IDE's local API to show a session's tab, as one does whose notification about the session the user clicked.</summary>
public class When_a_tool_asks_to_show_a_session(When_a_tool_asks_to_show_a_session.Story story) : IClassFixture<When_a_tool_asks_to_show_a_session.Story>
{
   public sealed class Story
   {
      public readonly int SandboxProcess;
      public readonly JsonNode Announcement;
      public readonly (int Status, int? Selected) BySession;
      public readonly (int Status, int? Selected) ByProcessAfterClear;
      public readonly (int Status, int? Selected) Unknown;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         var tabs = Enumerable.Range(0, 3).Select(_ => view.StartANewSession()).ToList();
         SandboxProcess = view.SandboxProcessId;
         Announcement = view.LocalApiAnnouncement!;

         view.Select(0);
         BySession = (view.AskTheLocalApiToShow(tabs[2].SessionId, claudeCodeProcess: null), view.SelectedTab);

         view.Select(0);
         view.Type(1, "/clear");
         view.WaitUntil(() => view.Tabs[1].SessionId != tabs[1].SessionId, "the second tab to run a new session");
         ByProcessAfterClear = (view.AskTheLocalApiToShow(tabs[1].SessionId, tabs[1].ClaudeCode!.ProcessId), view.SelectedTab);

         Unknown = (view.AskTheLocalApiToShow(Guid.NewGuid().ToString(), claudeCodeProcess: null), view.SelectedTab);
      }
   }

   [XF] public void the_IDE_announces_its_local_api_in_a_file_named_after_its_process() =>
      (story.Announcement["protocol"]!.GetValue<int>(), story.Announcement["processId"]!.GetValue<long>()).Must().Be((1, (long)story.SandboxProcess));

   [XF] public void it_selects_the_tab_that_runs_the_session() => story.BySession.Must().Be((200, (int?)2));
   [XF] public void it_finds_the_tab_by_its_claude_code_after_clear_gave_it_a_new_session() => story.ByProcessAfterClear.Must().Be((200, (int?)1));
   [XF] public void it_answers_404_for_a_session_none_of_its_tabs_runs_and_leaves_the_selection() => story.Unknown.Must().Be((404, (int?)1));
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
         for(var tab = 0; tab < 3; tab++)
         {
            view.StartANewSession();
            view.Say(tab, $"a line of session {tab}'s conversation");
         }

         view.ExitClaudeCode(0);
         view.Select(1); // Neither the first tab nor the last, which the view might select whatever was selected.
         view.WaitUntil(() => view.SavedSessions.Count == 3, "the three sessions to be saved");
         Before = view.Tabs;
         ShownBefore = view.IsShown;
         view.DeleteTheTranscriptOf(Before[2].SessionId!); // As archiving the session does: it runs on all the same.
         view.RestartTheIde(tabsToComeBack: 3);
         After = view.Tabs;
         ShownAfter = view.IsShown;
         SelectedAfter = view.SelectedTab;
         view.WaitUntil(() => view.Screen(1).Any(it => it.StartsWith("> ")), "the second tab to show its conversation");
         SecondTabsScreen = view.Screen(1);
      }
   }

   [XF] public void every_tab_comes_back_as_it_was_its_session_having_run_on_in_its_session_host() =>
      story.After.Must().Satisfy(it => it.SequenceEqual(story.Before));

   [XF] public void a_tab_whose_claude_code_had_exited_comes_back_on_its_shell() =>
      story.After[0].Must().Satisfy(it => it.ShellProcessId == story.Before[0].ShellProcessId && it.ClaudeCode is null);

   // The session host repaints its console's screen for each new attach client.
   [XF] public void a_tab_shows_its_session_as_it_is() => story.SecondTabsScreen.Must().Contain("> a line of session 1's conversation");

   [XF] public void the_view_is_shown_again_as_it_was_when_the_IDE_closed() => (story.ShownBefore, story.ShownAfter).Must().Be((true, true));
   [XF] public void the_tab_that_was_selected_is_selected_again() => story.SelectedAfter.Must().Be(1);
}

/// <summary>The IDE restarts after its session hosts have ended, as after a reboot.</summary>
public class When_the_IDE_restarts_after_its_session_hosts_ended(When_the_IDE_restarts_after_its_session_hosts_ended.Story story)
   : IClassFixture<When_the_IDE_restarts_after_its_session_hosts_ended.Story>
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
         view.RestartTheIdeAsAfterAReboot(tabsToComeBack: 3);
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

   [XF] public void the_sessions_are_resumed_in_new_session_hosts() =>
      story.After.Select(it => it.SessionHost).Intersect(story.Before.Select(it => it.SessionHost)).Must().Satisfy(it => !it.Any());

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
      public readonly int? SelectedAfter;
      public readonly bool ShownBefore;
      public readonly bool ShownAfter;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         for(var tab = 0; tab < 3; tab++)
         {
            view.StartANewSession();
            view.Say(tab, $"a line of session {tab}'s conversation");
         }

         view.DeleteTheSessionRecordOfTheClaudeCodeIn(1); // As if the reload couldn't read it.
         view.ExitClaudeCode(2);
         view.Select(0); // Not the last tab to come back.
         Before = view.Tabs;
         ShownBefore = view.IsShown;
         view.ReloadThePlugin();
         view.WaitUntil(() => view.Tabs.Count == 3, "the three tabs to be back in the view");
         After = view.Tabs;
         SelectedAfter = view.SelectedTab;
         ShownAfter = view.IsShown;
      }
   }

   [XF] public void every_tab_comes_back_as_it_was_its_session_having_run_on_in_its_session_host() => story.After.Must().Satisfy(it => it.SequenceEqual(story.Before));

   [XF] public void that_includes_a_tab_whose_claude_codes_record_of_its_session_it_could_not_read() =>
      story.After[1].Must().Satisfy(it => it.ShellProcessId == story.Before[1].ShellProcessId && it.SessionId == story.Before[1].SessionId);

   [XF] public void and_a_tab_whose_claude_code_had_exited_on_its_shell() =>
      story.After[2].Must().Satisfy(it => it.ShellProcessId == story.Before[2].ShellProcessId && it.ClaudeCode is null);

   [XF] public void the_tab_that_was_selected_is_selected_again() => story.SelectedAfter.Must().Be(0);
   [XF] public void the_view_is_shown_as_before() => (story.ShownBefore, story.ShownAfter).Must().Be((true, true));
}
