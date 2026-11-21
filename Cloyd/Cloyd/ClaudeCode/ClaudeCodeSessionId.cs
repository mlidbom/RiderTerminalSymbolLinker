using System.Text.Json;
using System.Text.Json.Serialization;
using Cloyd.Sessions;

namespace Cloyd.ClaudeCode;

/// <summary>
/// The id Claude Code gives a session, such as <c>1d24f54b-3002-4132-8a65-4d7dc8977525</c>: what
/// <c>claude --resume</c> takes, and what names the session's transcript.
/// </summary>
[JsonConverter(typeof(ClaudeCodeSessionIdInJson))]
sealed record ClaudeCodeSessionId(string Value) : SessionId(Value)
{
   /// <summary>A <see cref="ClaudeCodeSessionId"/> in JSON: the id as a string.</summary>
   sealed class ClaudeCodeSessionIdInJson : JsonConverter<ClaudeCodeSessionId>
   {
      public override ClaudeCodeSessionId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => new(reader.GetString()!);

      public override void Write(Utf8JsonWriter writer, ClaudeCodeSessionId value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
   }
}
