using System.Text.Json.Serialization;
using ClaudeTooling.Hub.LocalApi.Contract.TypingIntoRunningSessions;

namespace Cloyd.Chat.Writing;

partial record PartOfAMessage
{
   /// <summary>
   /// An image Magnus pasted, saved as a PNG <paramref name="File"/> by <see cref="Pasting.PastedImages"/>: the hub pastes
   /// its path alone into Claude Code's box, which Claude Code takes for the image, and shows as <c>[Image #1]</c>.
   /// </summary>
   public sealed record PastedImage([property: JsonConverter(typeof(FileAsItsPath))] FileInfo File) : PartOfAMessage
   {
      public override PartInTheRequest InTheRequest() => new(PartInTheRequest.TheKindOfAPastedImage, Text: null, File.FullName);

      /// <summary>Whether its file is still there: one deleted, as those kept a week are, can no longer be sent.</summary>
      [JsonIgnore] public bool IsStillThere => System.IO.File.Exists(File.FullName);

      public bool Equals(PastedImage? other) => other is not null && File.FullName == other.File.FullName;

      public override int GetHashCode() => File.FullName.GetHashCode();
   }
}
