using System.Text.RegularExpressions;

namespace Cloyd.Links.Files;

/// <summary>
/// Finds the <see cref="FileReference"/>s in a line of a terminal's output. A run of the characters paths are written
/// with is one when it names a file of the project (<see cref="ProjectFiles"/>): no extension or other shape decides.
/// </summary>
/// <remarks>
/// It links bare names, as <c>App.axaml.cs</c>, partial paths, as <c>Shell\AppShell.cs</c>, absolute paths, either
/// separator, and a path glued to punctuation, as in <c>Update(C:\…\AppShell.cs)</c>. A character that paths aren't
/// written with, such as <c>%</c>, <c>(</c>, <c>#</c> or a space, ends the run, so that <c>#Vantage/…</c> links its
/// <c>Vantage/…</c>. One that they are written with, glued on as in <c>nonexistingVantage/…</c>, is part of the path,
/// which then names no file.<br/>
/// The symbol linker's <c>FileReferences.kt</c>, ported: Rider's terminal links the same paths.
/// </remarks>
/// <remarks>
/// The place in the file is part of the link: <c>:line[-lastLine][:column]</c>, as compilers write it, or
/// <c>#Lline[-[L]lastLine]</c>, as Rider's "add file to chat" does. Punctuation that ends a sentence after a path is no
/// part of it. A URL is no file reference: Iciclecreek's terminal links URLs itself.
/// </remarks>
static partial class FileReferences
{
   static readonly char[] PunctuationThatEndsASentence = ['.', ',', ';', ':', ')', ']', '}', '!', '?', '"', '\''];

   public static IReadOnlyList<FileReference> In(string line, ProjectFiles files) =>
      [.. RunOfThePathsCharacters().Matches(line).Select(it => ReferenceIn(it, files)).OfType<FileReference>()];

   static FileReference? ReferenceIn(Match run, ProjectFiles files)
   {
      if(SpansOfPathsAndUrls.IsAUrl(run.Value)) return null;
      var text = run.Value.TrimEnd(PunctuationThatEndsASentence);
      var place = PlaceAsCompilersWriteIt().Match(text) is { Success: true } asCompilersWriteIt ? asCompilersWriteIt : PlaceAsGitHubWritesIt().Match(text);
      var path = place.Success ? text[..place.Index] : text;
      return files.HasOneWhosePathEndsWith(path)
                ? new FileReference(new SpanOfALine(run.Index, run.Index + text.Length), path, place.Success ? PlaceNamedBy(place) : null)
                : null;
   }

   /// <summary>The place in the file that <paramref name="place"/> matched: its groups are the line, the last line and the column, alike in both forms.</summary>
   static PlaceInAFile PlaceNamedBy(Match place) => new(int.Parse(place.Groups[1].Value), NumberIn(place.Groups[2]), NumberIn(place.Groups[3]));

   static int? NumberIn(Group group) => group.Success ? int.Parse(group.Value) : null;

   /// <summary>
   /// The longest run of the characters paths are written with, and a GitHub line anchor right after it: else <c>#</c>
   /// ends a run, as whitespace, brackets, quotes and commas do.
   /// </summary>
   [GeneratedRegex(@"[\w.\-+~/\\:]+(?:#L\d+(?:-L?\d+)?)?")]
   private static partial Regex RunOfThePathsCharacters();

   /// <summary><c>:line</c>, <c>:line-lastLine</c>, <c>:line:column</c> or <c>:line-lastLine:column</c> at the end, up to nine digits each, so that each fits an <see cref="int"/>.</summary>
   [GeneratedRegex(@":(\d{1,9})(?:-(\d{1,9}))?(?::(\d{1,9}))?$")]
   private static partial Regex PlaceAsCompilersWriteIt();

   /// <summary><c>#Lline</c> or <c>#Lline-[L]lastLine</c> at the end, as in <c>Desktop.cs#L136-138</c>: no column.</summary>
   [GeneratedRegex(@"#L(\d{1,9})(?:-L?(\d{1,9}))?$")]
   private static partial Regex PlaceAsGitHubWritesIt();
}
