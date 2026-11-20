using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cloyd.Hub;

/// <summary>
/// The id Claude Code gives a session, such as <c>1d24f54b-3002-4132-8a65-4d7dc8977525</c>: what
/// <c>claude --resume</c> takes, and what names the session's transcript.
/// </summary>
[JsonConverter(typeof(ClaudeSessionIdInJson))]
readonly record struct ClaudeSessionId(string Value)
{
   public override string ToString() => Value;

   /// <summary>A <see cref="ClaudeSessionId"/> in JSON: the id as a string.</summary>
   sealed class ClaudeSessionIdInJson : JsonConverter<ClaudeSessionId>
   {
      public override ClaudeSessionId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => new(reader.GetString()!);

      public override void Write(Utf8JsonWriter writer, ClaudeSessionId value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
   }
}
