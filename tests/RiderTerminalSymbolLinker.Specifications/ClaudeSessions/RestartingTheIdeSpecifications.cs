using System.Text.Json.Nodes;
using Compze.Must;
using Compze.xUnitBDD;
using Xunit;

// ReSharper disable InconsistentNaming

namespace RiderTerminalSymbolLinker.Specifications.ClaudeSessions;

// Claude Sessions' local API, as the tool for restarting Rider uses it (~/.claude/dev_docs/restarting-rider.md): the
// windows of the IDE's projects, and the IDE restarting itself when asked.

public class When_a_tool_asks_the_ide_to_restart(When_a_tool_asks_the_ide_to_restart.Story story) : IClassFixture<When_a_tool_asks_the_ide_to_restart.Story>
{
   public sealed class Story
   {
      public readonly string ProjectFolder;
      public readonly int ProcessBefore;
      public readonly JsonNode WindowBefore;
      public readonly int FromAnotherHost;
      public readonly int WithoutTheToken;
      public readonly (int Status, JsonNode? Body) Asked;
      public readonly int ProcessAfter;
      public readonly JsonNode WindowAfter;
      public readonly IReadOnlyList<SessionTab> TabsBefore;
      public readonly IReadOnlyList<SessionTab> TabsAfter;
      public readonly ChatSeen ChatAfter;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         var chat = view.ChatOf(0);
         chat.SayTheStatus("IDLE");
         chat.SeenOnce(it => it.Card == "CHAT", "the tab to show the chat");
         chat.Type("Written before the restart");
         ProjectFolder = view.ProjectFolder;
         ProcessBefore = view.SandboxProcessId;
         WindowBefore = view.AskTheLocalApi("GET", "/windows").Body!["windows"]!.AsArray().Single()!;
         FromAnotherHost = view.AskTheLocalApi("GET", "/windows", host: "claude-sessions.example").Status;
         var restart = new { why = "A specification restarts it", askedBy = "the specifications" };
         WithoutTheToken = view.AskTheLocalApi("POST", "/restart", restart).Status;
         TabsBefore = view.Tabs;
         Asked = view.AskTheLocalApi("POST", "/restart", restart, withTheToken: true);
         view.FollowTheIdesRestart(ProcessBefore, tabsToComeBack: 1);
         ProcessAfter = view.SandboxProcessId;
         WindowAfter = view.AskTheLocalApi("GET", "/windows").Body!["windows"]!.AsArray().Single()!;
         TabsAfter = view.Tabs;
         ChatAfter = view.ChatOf(0).SeenOnce(it => it.MessageBox != "", "the box to hold what it held");
      }
   }

   [XF] public void the_ides_windows_name_each_ones_project_by_its_folder() => Path.GetFullPath(story.WindowBefore["project"]!.GetValue<string>()).Must().Be(story.ProjectFolder);
   [XF] public void and_its_native_handle() => story.WindowBefore["window"]!.GetValue<long>().Must().Satisfy(it => it != 0);
   [XF] public void and_the_virtual_desktop_its_on() => story.WindowBefore["desktop"]!.GetValue<string>().Must().Satisfy(it => Guid.TryParse(it, out _));
   [XF] public void a_request_that_names_another_host_is_refused() => story.FromAnotherHost.Must().Be(403);
   [XF] public void a_restart_without_the_announcements_token_is_refused() => story.WithoutTheToken.Must().Be(401);
   [XF] public void with_it_the_restart_is_on_its_way() => story.Asked.Status.Must().Be(202);
   [XF] public void the_ide_restarts_as_another_process() => story.ProcessAfter.Must().Satisfy(it => it != story.ProcessBefore);
   [XF] public void with_its_projects_window_back() => story.WindowAfter["project"]!.GetValue<string>().Must().Be(story.WindowBefore["project"]!.GetValue<string>());
   [XF] public void on_the_virtual_desktop_it_was_on() => story.WindowAfter["desktop"]!.GetValue<string>().Must().Be(story.WindowBefore["desktop"]!.GetValue<string>());

   [XF] public void with_its_tab_attached_to_its_session_again() =>
      story.TabsAfter.Single().Must().Satisfy(it => it.SessionHost == story.TabsBefore.Single().SessionHost && it.SessionId == story.TabsBefore.Single().SessionId);

   [XF] public void and_what_its_chats_box_held() => story.ChatAfter.MessageBox.Must().Be("Written before the restart");
}

/// <summary>
///   The tool for restarting Rider end to end: the sandbox's hub restarts the sandbox Rider through Claude Sessions' local
///   API, and finds it back. Asked as for a button Magnus presses, so with no prompt of his: an agent's request shows him
///   one while a Rider's window is in front, as the sandbox Rider's may be once it has started.
/// </summary>
public class When_the_hub_restarts_rider(When_the_hub_restarts_rider.Story story) : IClassFixture<When_the_hub_restarts_rider.Story>
{
   public sealed class Story
   {
      public readonly int ProcessBefore;
      public readonly IReadOnlyList<SessionTab> TabsBefore;
      public readonly RiderRestartByTheHub Restart;
      public readonly int ProcessAfter;
      public readonly IReadOnlyList<SessionTab> TabsAfter;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.StartANewSession();
         ProcessBefore = view.SandboxProcessId;
         TabsBefore = view.Tabs;
         Restart = view.HaveTheHubRestartTheIde("A specification of Claude Sessions has the hub restart Rider", tabsToComeBack: 1);
         ProcessAfter = view.SandboxProcessId;
         TabsAfter = view.Tabs;
      }
   }

   [XF] public void the_hub_says_rider_is_back() => story.Restart.Outcome.Must().Be("back");
   [XF] public void as_restarted_and_back() => story.Restart.Headline.Must().Be("Restarted and back");
   [XF] public void the_ide_restarted_as_another_process() => story.ProcessAfter.Must().Satisfy(it => it != story.ProcessBefore);

   [XF] public void with_its_tab_attached_to_its_session_again() =>
      story.TabsAfter.Single().Must().Satisfy(it => it.SessionHost == story.TabsBefore.Single().SessionHost && it.SessionId == story.TabsBefore.Single().SessionId);
}

/// <summary>
///   Whether Rider's own restart reopens every solution that was open, each in its window, which the tool for restarting
///   Rider relies on: Rider restarts without the command line it started with, and reopens the solutions it had open.
/// </summary>
public class When_the_ide_restarts_itself_with_three_solutions_open(When_the_ide_restarts_itself_with_three_solutions_open.Story story)
   : IClassFixture<When_the_ide_restarts_itself_with_three_solutions_open.Story>
{
   public sealed class Story
   {
      static readonly TimeSpan HowLongOpeningASolutionMayTake = TimeSpan.FromMinutes(2);

      public readonly IReadOnlyList<string> Open;
      public readonly IReadOnlyList<string> Before;
      public readonly IReadOnlyList<string> After;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         string[] copies = ["Second", "Third"];
         Open = [.. new[] { view.ProjectFolder }.Concat(copies.Select(view.OpenAnotherCopyOfTheFixtureSolution)).Order()];
         TheClaudeSessionsView.WaitUntil(() => view.ProjectWindows.Count == 3, "the three solutions' windows to show", HowLongOpeningASolutionMayTake);
         view.EndTheLaunchersOfTheCopies();
         Before = view.ProjectWindows;
         try
         {
            view.RestartTheIdeThroughItsLocalApi();
            TheClaudeSessionsView.WaitUntil(() => view.ProjectWindows.Count == 3, "the three solutions' windows to show again", HowLongOpeningASolutionMayTake);
            After = view.ProjectWindows;
         }
         catch(Exception)
         {
            view.CloseTheCopiesOfTheFixtureSolution(copies); // Before the failure is reported: the next story needs the sandbox as it was.
            throw;
         }
         view.CloseTheCopiesOfTheFixtureSolution(copies);
      }
   }

   [XF] public void before_the_restart_each_solution_has_its_window() => story.Before.Must().Satisfy(it => it.SequenceEqual(story.Open));
   [XF] public void after_it_each_has_its_window_again() => story.After.Must().Satisfy(it => it.SequenceEqual(story.Before));
}
