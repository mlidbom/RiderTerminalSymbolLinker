using Compze.Must;
using Compze.xUnitBDD;
using Xunit;

// ReSharper disable InconsistentNaming

namespace RiderTerminalSymbolLinker.Specifications.ClaudeSessions;

// Claude Sessions' local API, as Cloyd uses it when Magnus clicks a link in its chat (~/.claude/dev_docs/cloyd/
// cloyd-as-a-dotnet-application.md, "Clickable file paths and C# symbols"): opening a file in an editor of a project's
// window, at a line and a column.

public class When_a_tool_asks_the_ide_to_open_a_file(When_a_tool_asks_the_ide_to_open_a_file.Story story) : IClassFixture<When_a_tool_asks_the_ide_to_open_a_file.Story>
{
   public sealed class Story
   {
      public readonly int WithoutTheToken;
      public readonly int OfAProjectNotOpen;
      public readonly int OfNoFile;
      public readonly (int Status, long? Window) Opened;
      public readonly long WindowOfTheProject;
      public readonly (string File, int CaretLine, int CaretColumn)? SelectedEditor;
      public readonly int OpenedInTheBackground;
      public readonly IReadOnlyList<string> FilesOpen;
      public readonly int FilesOpenOfAProjectNotOpen;

      public Story()
      {
         var ide = new ClaudeSessionsInTheSandbox();
         var circle = Path.Combine(ide.ProjectFolder, "Geometry", "Shapes", "Circle.cs");
         WithoutTheToken = ide.AskTheLocalApi("POST", "/files/open", new { project = ide.ProjectFolder, file = circle, line = 8, column = 17 }).Status;
         OfAProjectNotOpen = ide.AskTheLocalApi("POST", "/files/open", new { project = Path.Combine(ide.ProjectFolder, "NoSuchProject"), file = circle }, withTheToken: true).Status;
         OfNoFile = ide.AskTheLocalApi("POST", "/files/open", new { project = ide.ProjectFolder, file = Path.Combine(ide.ProjectFolder, "NoSuchFile.cs") }, withTheToken: true).Status;
         var opened = ide.AskTheLocalApi("POST", "/files/open", new { project = ide.ProjectFolder, file = circle, line = 8, column = 17 }, withTheToken: true);
         Opened = (opened.Status, opened.Body?["window"]?.GetValue<long>());
         WindowOfTheProject = ide.AskTheLocalApi("GET", "/windows").Body!["windows"]!.AsArray().Single()!["window"]!.GetValue<long>();
         SelectedEditor = ide.SelectedEditor;
         var drawing = Path.Combine(ide.ProjectFolder, "Geometry", "Shapes", "Drawing.cs");
         OpenedInTheBackground = ide.AskTheLocalApi("POST", "/files/open", new { project = ide.ProjectFolder, file = drawing, focus = false }, withTheToken: true).Status;
         FilesOpen = [.. ide.AskTheLocalApi("GET", $"/files/opened?project={Uri.EscapeDataString(ide.ProjectFolder)}").Body!["files"]!.AsArray()
                           .Select(it => Path.GetRelativePath(ide.ProjectFolder, it!.GetValue<string>()).Replace('/', Path.DirectorySeparatorChar))];
         FilesOpenOfAProjectNotOpen = ide.AskTheLocalApi("GET", $"/files/opened?project={Uri.EscapeDataString(Path.Combine(ide.ProjectFolder, "NoSuchProject"))}").Status;
      }
   }

   [XF] public void a_request_without_the_announcements_token_is_refused() => story.WithoutTheToken.Must().Be(401);
   [XF] public void a_file_of_a_project_the_ide_doesnt_show_isnt_found() => story.OfAProjectNotOpen.Must().Be(404);
   [XF] public void a_path_with_no_file_isnt_found() => story.OfNoFile.Must().Be(404);
   [XF] public void with_the_token_a_file_of_a_project_it_shows_opens() => story.Opened.Status.Must().Be(200);
   [XF] public void the_answer_names_the_window_it_opened_in_for_the_asker_to_bring_forward() => story.Opened.Window.Must().Be(story.WindowOfTheProject);
   [XF] public void the_selected_editor_shows_the_file_at_the_line_and_column_asked_for() => story.SelectedEditor.Must().Be(("Geometry/Shapes/Circle.cs", 8, 17));
   [XF] public void a_file_asked_for_with_no_focus_opens_too() => story.OpenedInTheBackground.Must().Be(200);
   [XF] public void the_files_open_in_the_projects_window_are_listed() => story.FilesOpen.Must().Satisfy(it => it.Contains(Path.Combine("Geometry", "Shapes", "Circle.cs")) && it.Contains(Path.Combine("Geometry", "Shapes", "Drawing.cs")));
   [XF] public void the_files_open_of_a_project_the_ide_doesnt_show_arent_found() => story.FilesOpenOfAProjectNotOpen.Must().Be(404);
}
