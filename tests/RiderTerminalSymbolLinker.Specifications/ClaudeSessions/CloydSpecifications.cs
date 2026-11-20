using Compze.Must;
using Compze.xUnitBDD;
using Xunit;

// ReSharper disable InconsistentNaming

namespace RiderTerminalSymbolLinker.Specifications.ClaudeSessions;

/// <summary>
///   Claude Sessions starts in the sandbox Rider with the fixture solution open, as when the project opens, while no Cloyd
///   application runs: it runs the sandbox's <c>Cloyd.exe --project</c> for the project, which starts the application.
/// </summary>
/// <remarks>
///   Whether the application breaks away from the IDE's job isn't seen here: the sandbox Rider runs in no job, as the
///   tools start it, where Magnus's IDEs run in the one explorer starts them in. Cloyd's own specification of a job that
///   ends everything in it as it closes covers it.
/// </remarks>
public class When_claude_sessions_starts_with_a_project_open_and_no_cloyd_running(When_claude_sessions_starts_with_a_project_open_and_no_cloyd_running.Story story)
   : IClassFixture<When_claude_sessions_starts_with_a_project_open_and_no_cloyd_running.Story>
{
   public sealed class Story
   {
      public readonly string ProjectFolder;
      public readonly IReadOnlyList<string> ProjectsCloydShows;
      public readonly string Title;

      public Story()
      {
         var ide = new ClaudeSessionsInTheSandbox();
         ProjectFolder = ide.ProjectFolder;
         ide.StartClaudeSessionsAgainWithNoCloydRunning();
         ProjectsCloydShows = ide.ProjectsCloydShows;
         Title = ide.CloydsWindows.Single()["title"]!.GetValue<string>();
      }
   }

   [XF] public void cloyd_shows_the_projects_window() => story.ProjectsCloydShows.Must().SequenceEqual([story.ProjectFolder]);
   [XF] public void titled_after_the_project() => story.Title.Must().Be($"Cloyd: {Path.GetFileName(story.ProjectFolder)}");
}
