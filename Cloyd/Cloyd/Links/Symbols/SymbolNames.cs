namespace Cloyd.Links.Symbols;

/// <summary>
/// The names of the C# types of a project's solutions and of their members: what a word in a terminal's output must be to
/// link to a symbol (<see cref="SymbolReferences"/>). Each member is known with its type, so that
/// <c>ApplicationWindow.ForceToFront</c> links as one symbol only when that type has that member.
/// </summary>
sealed class SymbolNames
{
   readonly HashSet<string> _namesOfTypesAndMembers;
   readonly HashSet<MemberOfAType> _membersOfTypes;

   public SymbolNames(IEnumerable<string> namesOfTypes, IEnumerable<MemberOfAType> membersOfTypes)
   {
      _membersOfTypes = [.. membersOfTypes];
      _namesOfTypesAndMembers = [.. namesOfTypes, .. _membersOfTypes.Select(it => it.Member)];
   }

   /// <summary>Whether <paramref name="word"/> is the name of a type, or of a member of one.</summary>
   public bool IsTheNameOfATypeOrMember(string word) => _namesOfTypesAndMembers.Contains(word);

   public bool Has(MemberOfAType member) => _membersOfTypes.Contains(member);
}
