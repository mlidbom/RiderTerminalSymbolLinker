using Cloyd.Links;
using Cloyd.Links.Symbols;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Links;

/// <summary>
/// How <see cref="SymbolReferences"/> carves the C# symbols a line names out of it: words that name a type or member,
/// and a type's member, as in <c>ApplicationWindow.ForceToFront</c>, as one symbol where the type has it. The symbol
/// linker's <c>SymbolReferencesTest.kt</c>, ported.
/// </summary>
public class Finding_the_symbols_a_line_names
{
   static readonly SymbolNames Symbols = new(
      ["ApplicationWindow", "NativeWindow", "Logger", "LogEntry"],
      [new MemberOfAType("ApplicationWindow", "ForceToFront"), new MemberOfAType("NativeWindow", "ForceToFront"), new MemberOfAType("LogEntry", "Info"), new MemberOfAType("LogEntry", "Id")]);

   [XF] public void a_types_member_links_as_one_symbol() =>
      SymbolReferences.In("call ApplicationWindow.ForceToFront() now", Symbols).Must().SequenceEqual([new SymbolReference(new SpanOfALine(5, 35), "ApplicationWindow.ForceToFront")]);

   [XF] public void two_names_joined_by_a_dot_that_are_no_types_member_link_each_on_its_own() =>
      SymbolReferences.In("Logger.Info", Symbols).Must().SequenceEqual([new SymbolReference(new SpanOfALine(0, 6), "Logger"), new SymbolReference(new SpanOfALine(7, 11), "Info")]);

   [XF] public void a_qualified_member_links_as_its_type_and_member_alone() =>
      SymbolReferences.In("Foo.Bar.ApplicationWindow.ForceToFront", Symbols).Must().SequenceEqual([new SymbolReference(new SpanOfALine(8, 38), "ApplicationWindow.ForceToFront")]);

   [XF] public void a_type_with_a_member_it_doesnt_have_links_as_the_type_alone() =>
      SymbolReferences.In("ApplicationWindow.Unknown", Symbols).Must().SequenceEqual([new SymbolReference(new SpanOfALine(0, 17), "ApplicationWindow")]);

   [XF] public void a_types_name_links() =>
      SymbolReferences.In("see ApplicationWindow here", Symbols).Must().SequenceEqual([new SymbolReference(new SpanOfALine(4, 21), "ApplicationWindow")]);

   [XF] public void a_name_of_two_letters_doesnt_link_on_its_own() => SymbolReferences.In("the Id field", Symbols).Must().BeEmpty();

   [XF] public void prose_names_no_symbol() => SymbolReferences.In("just some prose here", Symbols).Must().BeEmpty();

   [XF] public void a_name_in_a_path_is_part_of_the_path() => SymbolReferences.In("src/ApplicationWindow.cs", Symbols).Must().BeEmpty();
}
