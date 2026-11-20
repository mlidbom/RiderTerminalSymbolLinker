using Cloyd.Links.Files;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Links;

/// <summary>
/// Which of a project's files a path names (<see cref="ProjectFiles"/>): those whose path it is the end of, every segment
/// lining up. The symbol linker's <c>SolutionFileIndexTest.kt</c>, ported.
/// </summary>
public class Finding_the_files_a_path_names
{
   const string AppShell = "C:/Dev/Virtual-Desktop-Grid-Switcher/Vantage/Shell/AppShell.cs";
   const string ProgramOfTheClient = "C:/Dev/Proj/Client/Program.cs";
   const string ProgramOfTheServer = "C:/Dev/Proj/Server/Program.cs";

   static readonly ProjectFiles Files = new([AppShell, "C:/Dev/Proj/App.axaml.cs", ProgramOfTheClient, ProgramOfTheServer]);

   [XF] public void a_bare_name_names_its_file() => Files.WhosePathEndsWith("AppShell.cs").Must().SequenceEqual([AppShell]);

   [XF] public void a_name_with_several_dots_names_its_file() => Files.HasOneWhosePathEndsWith("App.axaml.cs").Must().BeTrue();

   [XF] public void case_is_ignored() => Files.WhosePathEndsWith("APPSHELL.CS").Must().SequenceEqual([AppShell]);

   [XF] public void a_partial_path_names_the_file_whose_path_it_ends() => Files.WhosePathEndsWith("Vantage/Shell/AppShell.cs").Must().SequenceEqual([AppShell]);

   [XF] public void the_full_path_names_its_file() => Files.WhosePathEndsWith(AppShell).Must().SequenceEqual([AppShell]);

   [XF] public void a_path_with_backslashes_names_the_file_listed_with_slashes() =>
      Files.WhosePathEndsWith(@"C:\Dev\Virtual-Desktop-Grid-Switcher\Vantage\Shell\AppShell.cs").Must().SequenceEqual([AppShell]);

   [XF] public void a_path_with_a_folder_the_file_isnt_in_names_nothing() => Files.HasOneWhosePathEndsWith("nonexisting/Vantage/Shell/AppShell.cs").Must().BeFalse();

   [XF] public void a_path_with_letters_glued_to_its_first_folder_names_nothing() => Files.HasOneWhosePathEndsWith("nonexistingVantage/Shell/AppShell.cs").Must().BeFalse();

   [XF] public void a_name_under_a_folder_with_no_file_of_that_name_names_nothing() => Files.HasOneWhosePathEndsWith("Client/AppShell.cs").Must().BeFalse();

   [XF] public void a_name_the_project_has_no_file_of_names_nothing() => Files.HasOneWhosePathEndsWith("DoesNotExist.cs").Must().BeFalse();

   [XF] public void an_empty_path_names_nothing() => Files.HasOneWhosePathEndsWith("").Must().BeFalse();

   [XF] public void a_path_that_ends_with_a_separator_names_a_folder_not_a_file() => Files.HasOneWhosePathEndsWith("Vantage/Shell/").Must().BeFalse();

   [XF] public void a_bare_name_names_every_file_of_that_name() =>
      Files.WhosePathEndsWith("Program.cs").Must().SequenceEqual([ProgramOfTheClient, ProgramOfTheServer]);

   [XF] public void a_folder_before_a_shared_name_picks_the_one_file_in_it() =>
      Files.WhosePathEndsWith("Server/Program.cs").Must().SequenceEqual([ProgramOfTheServer]);

   [XF] public void a_folder_with_no_file_of_a_shared_name_names_nothing() => Files.HasOneWhosePathEndsWith("Other/Program.cs").Must().BeFalse();
}
