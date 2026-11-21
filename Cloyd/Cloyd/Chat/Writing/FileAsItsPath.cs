using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cloyd.Chat.Writing;

/// <summary>Saves a file as its full path, and reads it back from that: JSON can't make a <see cref="FileInfo"/> of its own.</summary>
sealed class FileAsItsPath : JsonConverter<FileInfo>
{
   public override FileInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => new(reader.GetString()!);

   public override void Write(Utf8JsonWriter writer, FileInfo value, JsonSerializerOptions options) => writer.WriteStringValue(value.FullName);
}
