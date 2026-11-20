using System.Text.RegularExpressions;
using Cloyd.Links.Symbols;

namespace Cloyd.Links;

/// <summary>
/// The spans of a line of a terminal's output that are a path or a URL, where a word is no
/// <see cref="SymbolReference"/>: <c>SymbolIndex</c> in <c>…/csl/SymbolIndex.kt</c> is part of a path, whose link is to
/// the file.
/// </summary>
/// <remarks>
/// A run of text with a separator in it is a path or a URL only with a sign of one: a file's extension, a start such as
/// a drive, a separator, <c>~/</c>, <c>./</c> or <c>../</c>, or a URL's scheme. Words joined by separators, as in
/// <c>IsSupported/CanName/CanSetPerDesktopWallpaper</c>, are no path, and stay links to their symbols. A bare name with a
/// file's extension, such as <c>SymbolIndex.kt</c>, is a file's.<br/>
/// The symbol linker's <c>PathSpans.kt</c>, ported: Rider's terminal links the same spans.
/// </remarks>
static partial class SpansOfPathsAndUrls
{
   /// <summary>The extensions that make a word with a dot in it the name of a file, so that <c>Logger.Info</c> is none.</summary>
   const string ExtensionsOfFiles = "cs|csproj|sln|slnx|kt|kts|java|xml|json|ya?ml|txt|md|props|targets|config|resx|razor|cshtml|js|ts|tsx|jsx|html?|css|py|go|rs|rb|sql|sh|ps1|gradle|properties|toml|ini";

   public static IReadOnlyList<SpanOfALine> In(string line) =>
      [.. RunWithASeparator().Matches(line).Where(it => IsAPathOrAUrl(it.Value)).Concat(NameOfAFile().Matches(line)).Select(SpanOfALine.Of)];

   /// <summary>Whether <paramref name="text"/> starts with a URL's scheme, as <c>https://</c> or <c>file://</c> do.</summary>
   public static bool IsAUrl(string text) => SchemeOfAUrl().IsMatch(text);

   static bool IsAPathOrAUrl(string runWithASeparator) => IsAUrl(runWithASeparator) || StartOfAPath().IsMatch(runWithASeparator) || NameOfAFile().IsMatch(runWithASeparator);

   /// <summary>A name with a file's extension, and the line and column after it that compilers write, as in <c>SymbolIndex.kt:12:5</c>.</summary>
   [GeneratedRegex($@"[\w.\-]+\.(?:{ExtensionsOfFiles})(?::\d+(?::\d+)?)?")]
   private static partial Regex NameOfAFile();

   /// <summary>The longest run of the characters of paths and URLs around a separator: a path or a URL, perhaps.</summary>
   [GeneratedRegex(@"[\w.:~\-]*[/\\][\w.:/\\~\-]*")]
   private static partial Regex RunWithASeparator();

   [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.\-]*://")]
   private static partial Regex SchemeOfAUrl();

   /// <summary>The start of an absolute or relative path: a drive, a separator, <c>~/</c>, <c>./</c> or <c>../</c>.</summary>
   [GeneratedRegex(@"^(?:[A-Za-z]:[/\\]|[/\\]|~[/\\]|\.{1,2}[/\\])")]
   private static partial Regex StartOfAPath();
}
