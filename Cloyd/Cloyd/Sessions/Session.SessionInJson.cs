using System.Text.Json;
using System.Text.Json.Serialization;
using Cloyd.ClaudeCode;

namespace Cloyd.Sessions;

abstract partial record Session
{
   /// <summary>
   /// A <see cref="Session"/> in JSON, as a saved tab holds it: its agent CLI, by the name the hub's local API gives it,
   /// such as <c>claudeCode</c>, its id and its name. A session saved with no agent CLI is Claude Code's, as a Cloyd saved
   /// them before it knew of other CLIs.
   /// </summary>
   sealed class SessionInJson : JsonConverter<Session>
   {
      const string AgentCli = "agentCli";
      const string Id = "id";
      const string Name = "name";
      const string ClaudeCode = "claudeCode";

      public override Session Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
      {
         var json = JsonElement.ParseValue(ref reader);
         var agentCli = json.TryGetProperty(AgentCli, out var named) ? named.GetString() : ClaudeCode;
         var id = json.GetProperty(Id).GetString()!;
         var name = json.TryGetProperty(Name, out var it) ? it.GetString() : null;
         return agentCli switch
         {
            ClaudeCode => new ClaudeCodeSession(new ClaudeCodeSessionId(id), name),
            _ => throw new JsonException($"The session {id} was saved as one of the agent CLI {agentCli}, which this Cloyd doesn't know.")
         };
      }

      public override void Write(Utf8JsonWriter writer, Session value, JsonSerializerOptions options)
      {
         writer.WriteStartObject();
         writer.WriteString(AgentCli, AgentCliOf(value));
         writer.WriteString(Id, value.Id.Value);
         writer.WriteString(Name, value.Name);
         writer.WriteEndObject();
      }

      static string AgentCliOf(Session session) => session switch
      {
         ClaudeCodeSession => ClaudeCode,
         _ => throw new ArgumentOutOfRangeException(nameof(session), session, "A session of an agent CLI that sessions aren't saved with yet.")
      };
   }
}
