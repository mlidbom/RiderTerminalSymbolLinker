using Cloyd.DotNetAnalysisAndRefactoring.Contract;
using Cloyd.Links.Symbols;

namespace Cloyd.DotNetAnalysisAndRefactoring;

/// <summary>
/// Gathers <see cref="SymbolNames"/> from the types <c>browse_namespace</c> lists: each type's name, and each of its
/// members with the type, so that a type's member in a terminal's output links only where the type has that member.
/// </summary>
/// <remarks>
/// A nested type is a member of the type it's nested in, and its own members are gathered too. The members of a C# 14
/// extension block are members of the static class that declares the block.<br/>
/// The symbol linker's <c>SymbolNamesGatherer.kt</c>, ported.
/// </remarks>
sealed class SymbolNamesGatherer
{
   const string KindOfAnExtensionBlock = "extension";

   /// <summary>
   /// The kinds of member that code calls by the member's own name. Not constructors, which take their type's name, nor
   /// indexers, which the MCP names <c>this</c>, nor operators, which keep their CLR names, such as <c>op_Addition</c>:
   /// linking those words would link text that names no member.
   /// </summary>
   static readonly HashSet<string> KindsCodeCallsByTheMembersName = ["method", "property", "field", "event"];

   readonly List<string> _namesOfTypes = [];
   readonly List<MemberOfAType> _membersOfTypes = [];

   public void Add(TypeListing type)
   {
      _namesOfTypes.Add(type.Name);
      foreach(var member in type.Members ?? []) Add(member, type);
      foreach(var nested in type.NestedTypes ?? [])
      {
         _membersOfTypes.Add(new MemberOfAType(type.Name, nested.Name));
         Add(nested);
      }
   }

   public SymbolNames Gathered() => new(_namesOfTypes, _membersOfTypes);

   void Add(MemberListing member, TypeListing type)
   {
      if(member.Kind == KindOfAnExtensionBlock)
         foreach(var inTheBlock in member.Members ?? []) Add(inTheBlock, type);
      else if(KindsCodeCallsByTheMembersName.Contains(member.Kind))
         _membersOfTypes.Add(new MemberOfAType(type.Name, member.Name!));
   }
}
