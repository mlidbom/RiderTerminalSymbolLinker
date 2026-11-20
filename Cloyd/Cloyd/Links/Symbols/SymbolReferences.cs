using System.Text.RegularExpressions;

namespace Cloyd.Links.Symbols;

/// <summary>
/// Finds the <see cref="SymbolReference"/>s in a line of a terminal's output: the words that name a C# type or member of
/// the project's solutions (<see cref="SymbolNames"/>). The names decide, not a word's shape: <c>_field</c>,
/// <c>ALL_CAPS</c> and <c>camelCase</c> link alike.
/// </summary>
/// <remarks>
/// Words joined by dots, as in <c>ApplicationWindow.ForceToFront</c>, link as one symbol, the member, where a type has
/// that member, looking from the left: in <c>Foo.Bar.ApplicationWindow.ForceToFront</c> the namespaces, which are no
/// symbols, are passed over. The words left over link each on its own, from
/// <see cref="ShortestWordThatLinksOnItsOwn"/> letters: two-letter names such as <c>Id</c> are too common in prose. A
/// word in a path or a URL is part of it (<see cref="SpansOfPathsAndUrls"/>).<br/>
/// The symbol linker's <c>SymbolReferences.kt</c>, ported: Rider's terminal links the same words.
/// </remarks>
static partial class SymbolReferences
{
   const int ShortestWordThatLinksOnItsOwn = 3;

   public static IReadOnlyList<SymbolReference> In(string line, SymbolNames symbols)
   {
      var spansOfPathsAndUrls = SpansOfPathsAndUrls.In(line);
      return [.. WordsJoinedByDots().Matches(line)
                                    .SelectMany(it => ReferencesIn(WordsOf(it), symbols))
                                    .Where(it => !spansOfPathsAndUrls.Any(span => span.Overlaps(it.Span)))];
   }

   static IEnumerable<SymbolReference> ReferencesIn(IReadOnlyList<Word> words, SymbolNames symbols)
   {
      var index = 0;
      while(index < words.Count)
      {
         if(index + 1 < words.Count && new MemberOfAType(words[index].Text, words[index + 1].Text) is var member && symbols.Has(member))
         {
            yield return new SymbolReference(new SpanOfALine(words[index].Span.Start, words[index + 1].Span.End), member.AsCodeWritesIt);
            index += 2;
            continue;
         }
         if(words[index].Text.Length >= ShortestWordThatLinksOnItsOwn && symbols.IsTheNameOfATypeOrMember(words[index].Text))
            yield return new SymbolReference(words[index].Span, words[index].Text);
         index++;
      }
   }

   static IReadOnlyList<Word> WordsOf(Match wordsJoinedByDots)
   {
      var words = new List<Word>();
      var start = wordsJoinedByDots.Index;
      foreach(var text in wordsJoinedByDots.Value.Split('.'))
      {
         words.Add(new Word(new SpanOfALine(start, start + text.Length), text));
         start += text.Length + 1;
      }
      return words;
   }

   [GeneratedRegex(@"[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*")]
   private static partial Regex WordsJoinedByDots();
}
