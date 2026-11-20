namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   A copy of these tools' build output, in a folder of its own named <paramref name="name"/> beside it, for a process
///   that runs them outside the agent's session, such as a scheduled task or an IDE's restarter: building the tools
///   meanwhile would be refused for files in use.
/// </summary>
sealed class CopyOfTheTools(string name)
{
   /// <summary>Where the copy is: inside the repository, so that the tools find it.</summary>
   public string Folder => Path.Combine(RepositoryRoot.Folder, "tools", "RiderTerminalSymbolLinker.DevelopmentTools", "bin", name);

   /// <summary>The copy's executable.</summary>
   public string Executable => Path.Combine(Folder, Path.GetFileName(Environment.ProcessPath!));

   /// <summary>Copies the tools' build output to <see cref="Folder"/>, in place of an earlier copy.</summary>
   public void Take()
   {
      if(Directory.Exists(Folder)) Directory.Delete(Folder, recursive: true);
      foreach(var file in Directory.GetFiles(AppContext.BaseDirectory, "*", SearchOption.AllDirectories))
      {
         var copy = Path.Combine(Folder, Path.GetRelativePath(AppContext.BaseDirectory, file));
         Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
         File.Copy(file, copy);
      }
   }
}
