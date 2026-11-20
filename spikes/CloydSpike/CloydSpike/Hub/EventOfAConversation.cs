using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

namespace CloydSpike.Hub;

/// <summary>
/// One event of <c>GET /sessions/{sessionId}/conversation/stream</c>: <c>message</c>, <c>tool-result</c>, <c>rewind</c>
/// or <c>caught-up</c>, with its data.
/// </summary>
sealed record EventOfAConversation(string Name, JsonNode Data)
{
   /// <summary>The events of the Server-Sent Events <paramref name="events"/> holds, as they come: comments, such as keep-alives, left out.</summary>
   public static async IAsyncEnumerable<EventOfAConversation> ReadFrom(TextReader events, [EnumeratorCancellation] CancellationToken cancellation)
   {
      string? name = null;
      while(await events.ReadLineAsync(cancellation) is { } line)
      {
         if(line.StartsWith("event: ")) name = line["event: ".Length..];
         else if(line.StartsWith("data: ") && name is not null) yield return new EventOfAConversation(name, JsonNode.Parse(line["data: ".Length..])!);
         else if(line.Length == 0) name = null;
      }
   }
}
