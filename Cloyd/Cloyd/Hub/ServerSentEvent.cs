using System.Runtime.CompilerServices;
using System.Text;

namespace Cloyd.Hub;

/// <summary>
/// One event of a stream of Server-Sent Events, as the hub's streams send them (<c>Content-Type: text/event-stream</c>):
/// its name, from its <c>event:</c> line, and its data, from its <c>data:</c> lines.
/// </summary>
readonly record struct ServerSentEvent(string Name, string Data)
{
   /// <summary>The name of an event that has no <c>event:</c> line, as the standard has it.</summary>
   const string NameOfAnEventNotNamed = "message";

   /// <summary>Each event <paramref name="stream"/> sends, until it ends: comments, such as keep-alives, are skipped.</summary>
   public static async IAsyncEnumerable<ServerSentEvent> ReadFrom(TextReader stream, [EnumeratorCancellation] CancellationToken cancellation)
   {
      var name = NameOfAnEventNotNamed;
      var data = new StringBuilder();
      while(await stream.ReadLineAsync(cancellation) is {} line)
      {
         if(line.Length == 0)
         {
            if(data.Length > 0) yield return new(name, data.ToString());
            name = NameOfAnEventNotNamed;
            data.Clear();
         }
         else if(!line.StartsWith(':'))
         {
            var (field, value) = FieldAndValueOf(line);
            if(field == "event") name = value;
            else if(field == "data") data.Append(data.Length > 0 ? "\n" : "").Append(value);
         }
      }

      static (string field, string value) FieldAndValueOf(string line)
      {
         var colon = line.IndexOf(':');
         if(colon < 0) return (line, "");
         var value = line[(colon + 1)..];
         return (line[..colon], value.StartsWith(' ') ? value[1..] : value);
      }
   }
}
