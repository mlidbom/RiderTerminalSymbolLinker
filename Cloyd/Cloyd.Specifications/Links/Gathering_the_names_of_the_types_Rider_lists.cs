using System.Text.Json;
using Cloyd.DotNetAnalysisAndRefactoring;
using Cloyd.DotNetAnalysisAndRefactoring.Contract;
using Cloyd.Links.Symbols;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Links;

/// <summary>
/// Which names a type that <c>browse_namespace</c> lists gives the <see cref="SymbolNames"/> of its solution
/// (<see cref="SymbolNamesGatherer"/>), read from the JSON the MCP answers. The symbol linker's
/// <c>SymbolNamesGathererTest.kt</c>, ported.
/// </summary>
public class Gathering_the_names_of_the_types_Rider_lists
{
   [XF] public void a_types_methods_properties_fields_and_events_are_its_members()
   {
      var names = Gathered("""
         { "kind": "class", "name": "Circle", "members": [
             { "access": "public", "kind": "property", "name": "Radius", "signature": "property Radius : double" },
             { "access": "public", "kind": "method", "name": "Area", "signature": "method Area() : double" },
             { "access": "private", "kind": "field", "name": "cache", "signature": "field cache : double" },
             { "access": "public", "kind": "event", "name": "Resized", "signature": "event Resized : EventHandler" }
           ], "nestedTypes": [] }
         """);
      new[] { "Circle", "Radius", "Area", "cache", "Resized" }.Must().Satisfy(it => it.All(names.IsTheNameOfATypeOrMember));
      new[] { "Radius", "Area", "cache", "Resized" }.Must().Satisfy(it => it.All(member => names.Has(new MemberOfAType("Circle", member))));
   }

   [XF] public void the_members_of_an_extension_block_are_members_of_the_class_that_declares_it()
   {
      var names = Gathered("""
         { "kind": "class", "name": "ShapeExtensions", "members": [
             { "access": "public", "kind": "method", "name": "TotalArea", "signature": "method TotalArea(shapes:IEnumerable<IShape>) : double" },
             { "kind": "extension", "signature": "extension(circle:Circle)", "receiver": { "name": "circle", "type": "Circle", "typeName": "Circle" },
               "members": [
                 { "access": "public", "kind": "property", "name": "Diameter", "signature": "property Diameter : double" },
                 { "access": "public", "kind": "method", "name": "Grown", "signature": "method Grown(by:double) : Circle" }
               ] }
           ], "nestedTypes": [] }
         """);
      new[] { "TotalArea", "Diameter", "Grown" }.Must().Satisfy(it => it.All(member => names.Has(new MemberOfAType("ShapeExtensions", member))));
   }

   [XF] public void a_nested_type_is_a_member_of_the_type_its_nested_in_with_members_of_its_own()
   {
      var names = Gathered("""
         { "kind": "class", "name": "Drawing", "members": [], "nestedTypes": [
             { "kind": "record", "name": "Stroke", "members": [
                 { "access": "public", "kind": "property", "name": "Width", "signature": "property Width : double" }
               ], "nestedTypes": [] }
           ] }
         """);
      names.Has(new MemberOfAType("Drawing", "Stroke")).Must().BeTrue();
      names.Has(new MemberOfAType("Stroke", "Width")).Must().BeTrue();
   }

   [XF] public void constructors_indexers_and_operators_give_no_names_as_code_doesnt_call_them_by_those()
   {
      var names = Gathered("""
         { "kind": "struct", "name": "Vector", "members": [
             { "access": "public", "kind": "constructor", "name": "Vector", "signature": "constructor .ctor(x:double)" },
             { "access": "public", "kind": "indexer", "name": "this", "signature": "indexer this[index:int] : double" },
             { "access": "public", "kind": "operator", "name": "op_Addition", "signature": "operator op_Addition(a:Vector, b:Vector) : Vector" }
           ], "nestedTypes": [] }
         """);
      names.Has(new MemberOfAType("Vector", "Vector")).Must().BeFalse();
      new[] { "this", "op_Addition" }.Must().Satisfy(it => !it.Any(names.IsTheNameOfATypeOrMember));
   }

   static SymbolNames Gathered(string type)
   {
      var gatherer = new SymbolNamesGatherer();
      gatherer.Add(JsonSerializer.Deserialize<TypeListing>(type, JsonSerializerOptions.Web)!);
      return gatherer.Gathered();
   }
}
