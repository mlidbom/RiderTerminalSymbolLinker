using Cloyd.Links;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Links;

/// <summary>
/// Which spans of a line <see cref="SpansOfPathsAndUrls"/> takes for a path or a URL, where no word links to a symbol.
/// Words joined by separators are no path, so that their symbols link. The symbol linker's <c>PathSpansTest.kt</c>,
/// ported.
/// </summary>
public class Finding_the_spans_of_paths_and_urls
{
   [XF] public void words_joined_by_slashes_are_no_path() => SpansOfPathsAndUrls.In("IsSupported/CanName/CanSetPerDesktopWallpaper").Must().BeEmpty();

   [XF] public void a_member_access_is_no_file() => SpansOfPathsAndUrls.In("Logger.Info").Must().BeEmpty();

   [XF] public void a_relative_path_to_a_source_file_covers_the_files_name() => Covers(".../csl/SymbolIndex.kt", "SymbolIndex").Must().BeTrue();

   [XF] public void a_relative_path_to_a_source_file_covers_its_folders() => Covers("src/main/Program.cs", "src").Must().BeTrue();

   [XF] public void a_bare_name_with_a_files_extension_is_a_files() => Covers("see SymbolIndex.kt here", "SymbolIndex").Must().BeTrue();

   [XF] public void an_absolute_windows_path_to_a_file_is_a_path() => Covers(@"C:\Dev\Vantage\App.cs", "Vantage").Must().BeTrue();

   [XF] public void an_absolute_windows_path_with_no_extension_is_a_path() => Covers(@"C:\Dev\Vantage", "Vantage").Must().BeTrue();

   [XF] public void an_absolute_unix_path_is_a_path() => Covers("/usr/local/Foo", "Foo").Must().BeTrue();

   [XF] public void a_path_from_the_home_folder_is_a_path() => Covers("~/bin/Foo", "Foo").Must().BeTrue();

   [XF] public void a_url_is_one() => Covers("https://github.com/x/Repo", "Repo").Must().BeTrue();

   [XF] public void prose_has_none() => Covers("call IsSupported now", "IsSupported").Must().BeFalse();

   /// <summary>Whether a span of a path or a URL in <paramref name="line"/> covers <paramref name="word"/>, where it first is.</summary>
   static bool Covers(string line, string word)
   {
      var start = line.IndexOf(word, StringComparison.Ordinal);
      start.Must().BeGreaterThanOrEqualTo(0);
      return SpansOfPathsAndUrls.In(line).Any(it => it.Overlaps(new SpanOfALine(start, start + word.Length)));
   }
}
