using Avalonia.Media.Imaging;

namespace Cloyd.Pasting;

/// <summary>
/// The images Magnus pasted into a session, each saved as a PNG file of its own in <paramref name="folder"/>: Claude Code
/// is handed an image by its file's path, pasted alone, which it takes for the image and shows as <c>[Image #1]</c>.
/// Each is kept for <see cref="HowLongOneIsKept"/>, as Claude Sessions kept those pasted into its chat.
/// </summary>
/// <remarks>
/// Handed on by a file, and not by Claude Code's own paste of an image, Alt+V on Windows, which reads the clipboard
/// itself: the clipboard Cloyd reads is the one Magnus pasted from, which on Avalonia's headless platform is no
/// clipboard of Windows' (claude-sessions.md, "The chat view: what the spikes found").
/// </remarks>
sealed class PastedImages(DirectoryInfo folder)
{
   static readonly TimeSpan HowLongOneIsKept = TimeSpan.FromDays(7);

   readonly DirectoryInfo _folder = folder;

   /// <summary>Saves <paramref name="image"/> as a PNG file of its own, and returns the file: those kept long enough are deleted first.</summary>
   public FileInfo Save(Bitmap image)
   {
      _folder.Create();
      DeleteThoseKeptLongEnough();
      var file = new FileInfo(Path.Combine(_folder.FullName, $"{DateTime.Now:yyyy-MM-dd-HHmmss}-{Guid.NewGuid():N}.png"));
      image.Save(file.FullName, PngBitmapEncoderOptions.Default);
      return file;
   }

   void DeleteThoseKeptLongEnough()
   {
      foreach(var kept in _folder.EnumerateFiles("*.png").Where(it => DateTime.UtcNow - it.LastWriteTimeUtc > HowLongOneIsKept))
         kept.Delete();
   }
}
