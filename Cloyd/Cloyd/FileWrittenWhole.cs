namespace Cloyd;

/// <summary>
/// A file that its writer replaces whole at once, writing it beside its place and moving it in, so that a reader never
/// finds half of it: such as the announcement of a local API, Cloyd's or an IDE's, or what a project window saved of its
/// tabs.
/// </summary>
/// <remarks>
/// The move holds the file with delete access for a moment. A reader that doesn't share delete access fails then, the
/// file being used by another process, as <c>File.ReadAllText</c> does; and while such a reader holds the file, the move
/// fails. So <see cref="Text"/> reads it sharing delete access, as every reader of such a file must. The tooling owner
/// measured it for the hub's announcement, with one writer and two readers for 5 s: about 2,300 of 50,000 reads by
/// <c>File.ReadAllText</c> failed, and none of 32,000 that shared delete access (2026-11-20).
/// </remarks>
sealed class FileWrittenWhole(string path)
{
   readonly string _path = path;

   /// <summary>What the file holds: null while there is none, as before it is first written, and once it is deleted.</summary>
   public string? Text()
   {
      try
      {
         using var reader = new StreamReader(new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
         return reader.ReadToEnd();
      }
      catch(Exception failure) when(failure is FileNotFoundException or DirectoryNotFoundException) //Moved in whole and deleted by another process: either may be at any moment.
      {
         return null;
      }
   }

   /// <summary>Replaces the file with one that holds <paramref name="text"/>, whole at once, creating its folder if need be.</summary>
   public void Write(string text)
   {
      Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
      var writing = $"{_path}.writing";
      File.WriteAllText(writing, text);
      File.Move(writing, _path, overwrite: true);
   }
}
