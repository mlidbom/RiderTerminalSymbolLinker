namespace RiderTerminalSymbolLinker.DevelopmentTools.Plugin;

/// <summary>Makes one folder hold exactly what another does, touching only the files that differ.</summary>
/// <remarks>
///   A running Rider maps its plugins' jars into memory, and Windows refuses to overwrite or delete a mapped file, but
///   lets it be moved. So a file that is replaced or removed is moved to a set-aside folder first, and deleted from
///   there by a later mirror, once nothing maps it any more.
/// </remarks>
sealed class FolderMirror
{
   readonly string _setAsideFolder;

   /// <param name="setAsideFolder">Where replaced and removed files go, on the same drive as the folders mirrored.</param>
   public FolderMirror(string setAsideFolder) => _setAsideFolder = setAsideFolder;

   /// <summary>
   ///   Whether a mirror added a file the destination didn't have, or removed one, rather than only replacing files.
   ///   A Rider reloads a plugin only when a file's content changes, so after such a mirror it needs a restart.
   /// </summary>
   public bool AddedOrRemovedAFile { get; private set; }

   /// <summary>
   ///   Copies each file of <paramref name="source"/> that <paramref name="destination"/> lacks or has with another size
   ///   or time, and removes what <paramref name="destination"/> has that <paramref name="source"/> doesn't. Returns
   ///   whether it changed anything.
   /// </summary>
   public bool Mirror(string source, string destination)
   {
      DeleteWhatNothingHoldsAnyMore();
      return MirrorFolder(source, destination);
   }

   /// <summary>
   ///   Whether <paramref name="destination"/> holds exactly what <paramref name="source"/> does, the same files with the
   ///   same sizes and times, as a <see cref="Mirror"/> would leave it.
   /// </summary>
   public static bool HoldTheSame(string source, string destination)
   {
      return Directory.Exists(destination)
          && NamesIn(Directory.GetFiles(source)).SetEquals(NamesIn(Directory.GetFiles(destination)))
          && NamesIn(Directory.GetDirectories(source)).SetEquals(NamesIn(Directory.GetDirectories(destination)))
          && Directory.GetFiles(source).All(it => IsTheSame(it, Path.Combine(destination, Path.GetFileName(it))))
          && Directory.GetDirectories(source).All(it => HoldTheSame(it, Path.Combine(destination, Path.GetFileName(it))));

      static HashSet<string> NamesIn(string[] paths) => new(paths.Select(it => Path.GetFileName(it)), StringComparer.OrdinalIgnoreCase);
   }

   bool MirrorFolder(string source, string destination)
   {
      var changed = false;
      Directory.CreateDirectory(destination);

      foreach(var file in Directory.GetFiles(source))
      {
         var copy = Path.Combine(destination, Path.GetFileName(file));
         if(IsTheSame(file, copy)) continue;
         if(File.Exists(copy)) SetAside(copy);
         else AddedOrRemovedAFile = true;
         File.Copy(file, copy);
         File.SetLastWriteTimeUtc(copy, File.GetLastWriteTimeUtc(file));
         changed = true;
      }

      foreach(var folder in Directory.GetDirectories(source))
         changed |= MirrorFolder(folder, Path.Combine(destination, Path.GetFileName(folder)));

      foreach(var extraFile in Directory.GetFiles(destination).Where(it => !File.Exists(Path.Combine(source, Path.GetFileName(it)))))
      {
         SetAside(extraFile);
         changed = AddedOrRemovedAFile = true;
      }

      foreach(var extraFolder in Directory.GetDirectories(destination).Where(it => !Directory.Exists(Path.Combine(source, Path.GetFileName(it)))))
      {
         Directory.Move(extraFolder, Path.Combine(_setAsideFolder, $"{Guid.NewGuid():N}-{Path.GetFileName(extraFolder)}"));
         changed = AddedOrRemovedAFile = true;
      }

      return changed;
   }

   void SetAside(string file)
   {
      Directory.CreateDirectory(_setAsideFolder);
      File.Move(file, Path.Combine(_setAsideFolder, $"{Guid.NewGuid():N}-{Path.GetFileName(file)}"));
   }

   void DeleteWhatNothingHoldsAnyMore()
   {
      if(!Directory.Exists(_setAsideFolder)) return;
      foreach(var file in Directory.GetFiles(_setAsideFolder, "*", SearchOption.AllDirectories))
      {
         try
         {
            File.Delete(file);
         }
         catch(IOException) // A Rider that hasn't unloaded the jar yet still maps it: a later mirror deletes it.
         {
         }
      }

      foreach(var folder in Directory.GetDirectories(_setAsideFolder).Where(it => Directory.GetFiles(it, "*", SearchOption.AllDirectories).Length == 0))
         Directory.Delete(folder, recursive: true);
   }

   static bool IsTheSame(string file, string copy) =>
      File.Exists(copy)
   && new FileInfo(file).Length == new FileInfo(copy).Length
   && File.GetLastWriteTimeUtc(file) == File.GetLastWriteTimeUtc(copy);
}
