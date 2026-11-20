using Cloyd.Links;
using Cloyd.Links.Files;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Links;

/// <summary>
/// How <see cref="FileReferences"/> carves the paths that name a project's files out of a line of a terminal's output,
/// with the place in the file that follows a path. The symbol linker's <c>FileReferencesTest.kt</c>, ported.
/// </summary>
public class Finding_the_files_a_line_names
{
   static readonly ProjectFiles Files = new([
      @"C:\Dev\Virtual-Desktop-Grid-Switcher\Vantage\Shell\AppShell.cs",
      @"C:\Dev\Proj\App.axaml.cs",
      @"C:\Dev\Proj\Client\Program.cs"
   ]);

   const string AbsolutePathOfAppShell = @"C:\Dev\Virtual-Desktop-Grid-Switcher\Vantage\Shell\AppShell.cs";

   [XF] public void a_bare_name_with_a_range_of_lines_links_with_the_range() =>
      TheOneIn("App.axaml.cs:63-71").Must().Be(new FileReference(new SpanOfALine(0, 18), "App.axaml.cs", new PlaceInAFile(63, 71, null)));

   [XF] public void a_name_with_a_line_links_with_the_line() =>
      TheOneIn("App.axaml.cs:63").Place.Must().Be(new PlaceInAFile(63, null, null));

   [XF] public void a_name_with_a_line_and_a_column_links_with_both() =>
      TheOneIn("App.axaml.cs:63:5").Place.Must().Be(new PlaceInAFile(63, null, 5));

   [XF] public void a_path_with_a_github_line_anchor_as_riders_add_file_to_chat_writes_it_links_with_the_lines_and_without_the_at_sign() =>
      TheOneIn(@"@Vantage\Shell\AppShell.cs#L136-138").Must().Be(new FileReference(new SpanOfALine(1, 35), @"Vantage\Shell\AppShell.cs", new PlaceInAFile(136, 138, null)));

   [XF] public void a_github_anchor_of_one_line_links_with_the_line() =>
      TheOneIn("App.axaml.cs#L63").Place.Must().Be(new PlaceInAFile(63, null, null));

   [XF] public void a_github_anchor_with_an_L_on_both_lines_links_with_the_range() =>
      TheOneIn("App.axaml.cs#L63-L71").Place.Must().Be(new PlaceInAFile(63, 71, null));

   [XF] public void punctuation_that_ends_a_sentence_is_no_part_of_the_link() =>
      TheOneIn("see App.axaml.cs:63.").Span.Must().Be(new SpanOfALine(4, 19));

   [XF] public void a_partial_path_keeps_its_separators() =>
      TheOneIn(@"Vantage\Shell\AppShell.cs").Path.Must().Be(@"Vantage\Shell\AppShell.cs");

   [XF] public void an_absolute_path_keeps_its_separators() =>
      TheOneIn(AbsolutePathOfAppShell).Path.Must().Be(AbsolutePathOfAppShell);

   [XF] public void a_path_glued_to_parentheses_links_without_them() =>
      TheOneIn($"Update({AbsolutePathOfAppShell})").Path.Must().Be(AbsolutePathOfAppShell);

   [XF] public void a_hash_before_a_path_is_no_part_of_it() =>
      TheOneIn("#Vantage/Shell/AppShell.cs").Path.Must().Be("Vantage/Shell/AppShell.cs");

   [XF] public void a_percent_sign_before_a_path_is_no_part_of_it() =>
      TheOneIn("%Vantage/Shell/AppShell.cs").Path.Must().Be("Vantage/Shell/AppShell.cs");

   [XF] public void an_arrow_before_a_path_is_no_part_of_it() =>
      TheOneIn("->Vantage/Shell/AppShell.cs").Path.Must().Be("Vantage/Shell/AppShell.cs");

   [XF] public void a_path_with_letters_glued_to_its_first_folder_names_no_file() =>
      FileReferences.In("nonexistingVantage/Shell/AppShell.cs", Files).Must().BeEmpty();

   [XF] public void a_url_is_left_to_the_terminal() =>
      FileReferences.In("https://github.com/x/AppShell.cs", Files).Must().BeEmpty();

   [XF] public void prose_names_no_file() =>
      FileReferences.In("just some prose here", Files).Must().BeEmpty();

   [XF] public void a_name_the_project_has_no_file_of_names_no_file() =>
      FileReferences.In("see RandomThing.cs over there", Files).Must().BeEmpty();

   static FileReference TheOneIn(string line)
   {
      var references = FileReferences.In(line, Files);
      references.Must().HaveCount(1);
      return references[0];
   }
}
