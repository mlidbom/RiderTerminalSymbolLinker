using System.Text.Json.Nodes;

namespace RiderTerminalSymbolLinker.Specifications;

/// <summary>
///   What a click on a terminal link did, as the plugin's observation channel tells it, with the fixture solution's files
///   given by their path within it, such as <c>Geometry/Shapes/Circle.cs</c>.
/// </summary>
/// <param name="Outcome">
///   <c>opened</c>, <c>picker</c>, <c>nothingFound</c>, <c>searchEverywhere</c>, or, when the line had no such link,
///   <c>noSuchLink</c>.
/// </param>
/// <param name="OpenedFile">The file the selected editor shows after the click, for <c>opened</c>.</param>
/// <param name="CaretLine">The line the selected editor's caret is on after the click, 1-based, for <c>opened</c>.</param>
/// <param name="CaretColumn">The column the selected editor's caret is in after the click, 1-based, for <c>opened</c>.</param>
/// <param name="SelectedLines">The first and last lines the selected editor's selection covers, if it has one.</param>
/// <param name="PickerTitle">The picker's title, for <c>picker</c>.</param>
/// <param name="PickerNote">What the picker says above its rows, if anything, for <c>picker</c>.</param>
/// <param name="PickerRows">The picker's rows, for <c>picker</c>.</param>
/// <param name="PickerLocation">Where on the screen the picker's top left corner is, for <c>picker</c>.</param>
/// <param name="ClickedAt">Where on the screen the link was clicked, for a link the terminal tells where.</param>
public sealed record ClickOutcome(
   string Outcome,
   string? OpenedFile,
   int? CaretLine,
   int? CaretColumn,
   IReadOnlyList<int>? SelectedLines,
   string? PickerTitle,
   string? PickerNote,
   IReadOnlyList<string>? PickerRows,
   (int X, int Y)? PickerLocation,
   (int X, int Y)? ClickedAt)
{
   internal static ClickOutcome From(JsonNode answer, string fixtureFolder)
   {
      var editor = answer["editor"];
      return new ClickOutcome(
         answer["outcome"]?.GetValue<string>() ?? throw new InvalidOperationException($"The observation channel answered without an outcome: {answer}"),
         editor?["file"]?.GetValue<string>() is {} file ? Path.GetRelativePath(fixtureFolder, file).Replace('\\', '/') : null,
         editor?["caretLine"]?.GetValue<int>(),
         editor?["caretColumn"]?.GetValue<int>(),
         editor?["selectedLines"]?.AsArray().Select(it => it!.GetValue<int>()).ToList(),
         answer["title"]?.GetValue<string>(),
         answer["note"]?.GetValue<string>(),
         answer["rows"]?.AsArray().Select(it => it!.GetValue<string>()).ToList(),
         PointIn(answer["location"]),
         PointIn(answer["clickedAt"]));
   }

   static (int X, int Y)? PointIn(JsonNode? point) => point is null ? null : (point["x"]!.GetValue<int>(), point["y"]!.GetValue<int>());
}
