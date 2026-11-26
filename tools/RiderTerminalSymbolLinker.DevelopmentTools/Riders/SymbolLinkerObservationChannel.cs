using System.Text.Json.Nodes;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   The symbol linker's <see cref="ObservationChannel"/>, <c>dev.magnus.csl.observation.ObservationChannel</c>: its
///   indexes, the links its filters make, and what a click on one does.
/// </summary>
/// <remarks><see cref="ObservationChannel.State"/> is each open project's symbol and file indexes: ready, and their sizes.</remarks>
public sealed class SymbolLinkerObservationChannel(int port) : ObservationChannel(port)
{
   /// <summary>The links the plugin's filters make in <paramref name="line"/> of terminal output.</summary>
   public JsonNode Links(string line, string? project = null) => Post("links", project, new { line });

   /// <summary>
   ///   Clicks the link whose text is <paramref name="link"/> in <paramref name="line"/>, and tells what the click did:
   ///   opened a file, and where the editor is; showed a picker, and its rows; found nothing; or fell back to Search
   ///   Everywhere.
   /// </summary>
   public JsonNode Click(string line, string link, string? project = null) => Post("click", project, new { line, link });

   /// <summary>Chooses <paramref name="row"/> of the picker the last click showed, as Enter does, and tells what it did.</summary>
   public JsonNode ChoosePickerRow(int row, string? project = null) => Post("picker/choose", project, new { }, $"row={row}");

   /// <summary>Closes the picker the last click showed.</summary>
   public JsonNode ClosePicker() => Post("picker/close", null, new { });

   /// <summary>
   ///   Paints the picker the last click showed, as it shows, into the PNG file <paramref name="pngFile"/>, and leaves it
   ///   open: a picture of it wherever the sandbox's window is, on another virtual desktop too.
   /// </summary>
   public JsonNode PicturePicker(string pngFile) => Post("picker/picture", null, new { }, $"file={Uri.EscapeDataString(Path.GetFullPath(pngFile))}");

   /// <summary>Closes every editor, so that a click opens its file afresh.</summary>
   public JsonNode CloseEveryEditor(string? project = null) => Post("editors/close-all", project, new { });
}
