using Compze.Must;
using Compze.xUnitBDD;

// ReSharper disable InconsistentNaming

namespace RiderTerminalSymbolLinker.Specifications.Links;

public class When_a_line_of_terminal_output_names_the_solutions_symbols_and_files(TheSandboxWithTheFixtureSolution sandbox)
{
   const string Line = "Circle.Area() and Diameter; Bounds is ambiguous; see Shapes/Circle.cs:7 and Bounds.cs";

   readonly IReadOnlyList<(string Text, string Kind)> _links = sandbox.LinksIn(Line);

   [XF] public void a_type_and_its_member_written_with_a_dot_link_as_one_symbol() => _links.Must().Contain(("Circle.Area", "symbol"));
   [XF] public void a_type_name_that_two_namespaces_share_links_as_a_symbol() => _links.Must().Contain(("Bounds", "symbol"));
   [XF] public void a_partial_path_with_a_line_links_as_a_file() => _links.Must().Contain(("Shapes/Circle.cs:7", "file"));
   [XF] public void a_file_name_that_two_files_share_links_as_a_file() => _links.Must().Contain(("Bounds.cs", "file"));
   [XF] public void a_word_that_names_no_symbol_does_not_link() => _links.Must().Satisfy(it => it.All(link => link.Text != "ambiguous"));

   [XF(Skip = "The .NET Analysis&Refactoring MCP that the sandbox runs, the Marketplace's 0.14.0, lists an extension block's property only as its accessor, get_Diameter. Its tools since 2026-11-17 list it as a property, which the plugin reads: unskip once the sandbox runs a release that has them.")]
   public void a_property_declared_in_a_csharp_14_extension_block_links_as_a_symbol() => _links.Must().Contain(("Diameter", "symbol"));
}
