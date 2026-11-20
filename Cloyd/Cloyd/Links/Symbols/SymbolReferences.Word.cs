namespace Cloyd.Links.Symbols;

static partial class SymbolReferences
{
   /// <summary>An identifier in a line, as C# writes one in ASCII, and where it is.</summary>
   readonly record struct Word(SpanOfALine Span, string Text);
}
