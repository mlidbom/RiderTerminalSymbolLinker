namespace Cloyd.Links.Symbols;

/// <summary>
/// A member of a C# type, by their names: <c>ForceToFront</c> of <c>ApplicationWindow</c>, which code that calls it writes
/// <see cref="AsCodeWritesIt"/>, <c>ApplicationWindow.ForceToFront</c>. A type nested in another is a member of it too.
/// </summary>
readonly record struct MemberOfAType(string Type, string Member)
{
   public string AsCodeWritesIt => $"{Type}.{Member}";
}
