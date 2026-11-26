using Compze.Must;
using Compze.xUnitBDD;

// ReSharper disable InconsistentNaming

namespace RiderTerminalSymbolLinker.Specifications.Links;

/// <summary>
///   Words joined by dots link as one symbol as far as they are the end of its qualified name, and two words or more that
///   are the end of a namespace's name link as the namespace, as Cloyd's chat links them (Magnus, 2026-11-26: "Port it.").
/// </summary>
public class When_a_line_of_terminal_output_names_symbols_by_their_qualified_names_and_namespaces(TheSandboxWithTheFixtureSolution sandbox)
{
   const string Line = "Geometry.Shapes.Circle.Area() and Layout.Bounds; see Foo.Bar.Circle.Area in Geometry.Shapes; the Id; Shapes alone";

   readonly IReadOnlyList<(string Text, string Kind)> _links = sandbox.LinksIn(Line);

   [XF] public void a_member_qualified_by_its_namespace_links_as_one_symbol() => _links.Must().Contain(("Geometry.Shapes.Circle.Area", "symbol"));
   [XF] public void a_type_qualified_by_part_of_its_namespace_links_as_one_symbol() => _links.Must().Contain(("Layout.Bounds", "symbol"));
   [XF] public void names_before_a_type_that_are_not_its_namespaces_are_left_out_of_the_link() => _links.Must().Contain(("Circle.Area", "symbol"));
   [XF] public void a_namespaces_name_links_as_a_namespace() => _links.Must().Contain(("Geometry.Shapes", "namespace"));
   [XF] public void a_name_of_two_letters_links_on_its_own() => _links.Must().Contain(("Id", "symbol"));
   [XF] public void one_name_of_a_namespace_alone_does_not_link() => _links.Must().Satisfy(it => it.All(link => link.Text != "Shapes"));
}
