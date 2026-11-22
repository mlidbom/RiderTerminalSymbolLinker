using Avalonia.Platform.Storage;

namespace Cloyd.Chat.Views;

/// <summary>Opens a web link in the browser or a local screenshot in the same viewer used for pasted images.</summary>
static class ReplyLinks
{
   /// <summary>Returns the system launch result, or null for a target this chat doesn't open.</summary>
   public static Task<bool>? Open(Uri target, ILauncher launcher)
   {
      if(!target.IsAbsoluteUri) return null;
      if(target.Scheme is "http" or "https") return launcher.LaunchUriAsync(target);
      if(!target.IsFile || target.IsUnc || !target.IsLoopback) return null;

      // System.Uri decodes file:/// URLs, but treats C:/... as a literal Windows path.
      var path = target.LocalPath;
      if(path.Contains('%') && !target.OriginalString.StartsWith("file:", StringComparison.OrdinalIgnoreCase) && !File.Exists(path))
         path = Uri.UnescapeDataString(path);
      return Path.GetExtension(path).ToLowerInvariant() switch
      {
         ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".tif" or ".tiff" or ".avif" or ".ico" =>
            launcher.LaunchFileInfoAsync(new FileInfo(path)),
         _ => null
      };
   }
}
