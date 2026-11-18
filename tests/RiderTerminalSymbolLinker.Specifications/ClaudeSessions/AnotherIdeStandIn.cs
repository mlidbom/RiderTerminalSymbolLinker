using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RiderTerminalSymbolLinker.Specifications.ClaudeSessions;

/// <summary>
///   A stand-in for another IDE that runs Claude Sessions, as the sandbox's Claude Sessions sees one: it announces a
///   local API in the sandbox's announcements folder, as this process, answers which sessions run in its tabs, and
///   records what it's asked to show.
/// </summary>
/// <remarks>
///   It speaks just enough HTTP/1.1 for the requests Claude Sessions sends another IDE: <c>GET /sessions</c> and
///   <c>POST /sessions/show</c>.
/// </remarks>
public sealed class AnotherIdeStandIn : IDisposable
{
   readonly TcpListener _listener = new(IPAddress.Loopback, 0);
   readonly string _announcement;
   readonly IReadOnlyList<(string Session, long Process)> _sessionsInItsTabs;
   readonly List<string> _askedToShow = [];
   volatile bool _stopped;

   /// <param name="announcementsFolder">Where the sandbox's Claude Sessions finds other IDEs' announcements.</param>
   /// <param name="sessionsInItsTabs">The sessions it says run in its tabs, each with its Claude Code's process.</param>
   public AnotherIdeStandIn(string announcementsFolder, params IReadOnlyList<(string Session, long Process)> sessionsInItsTabs)
   {
      _sessionsInItsTabs = sessionsInItsTabs;
      _listener.Start();
      new Thread(Serve) { IsBackground = true, Name = "Another IDE's local API" }.Start();
      Directory.CreateDirectory(announcementsFolder);
      _announcement = Path.Combine(announcementsFolder, $"{Environment.ProcessId}.json");
      File.WriteAllText(_announcement, new JsonObject { ["protocol"] = 1, ["processId"] = Environment.ProcessId, ["port"] = ((IPEndPoint)_listener.LocalEndpoint).Port }.ToJsonString());
   }

   /// <summary>The queries of the requests to show a session it got, such as <c>session=…&amp;process=…</c>, in their order.</summary>
   public IReadOnlyList<string> AskedToShow
   {
      get
      {
         lock(_askedToShow) return [.. _askedToShow];
      }
   }

   public void Dispose()
   {
      File.Delete(_announcement);
      _stopped = true;
      _listener.Stop();
   }

   void Serve()
   {
      while(true)
      {
         TcpClient connection;
         try
         {
            connection = _listener.AcceptTcpClient();
         }
         catch(SocketException) when(_stopped)
         {
            return;
         }
         catch(ObjectDisposedException) when(_stopped)
         {
            return;
         }
         using(connection) Answer(connection.GetStream());
      }
   }

   void Answer(NetworkStream stream)
   {
      var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
      var requestLine = reader.ReadLine()!.Split(' ');
      while(reader.ReadLine() is { Length: > 0 }) {} // The headers: the requests have no body.
      var (method, target) = (requestLine[0], requestLine[1]);
      var (status, body) = (method, target.Split('?')[0]) switch
      {
         ("GET", "/sessions") => (200, JsonSerializer.Serialize(new { sessions = _sessionsInItsTabs.Select(it => new { session = it.Session, process = it.Process }) })),
         ("POST", "/sessions/show") => Show(target.Split('?', 2).ElementAtOrDefault(1) ?? ""),
         _ => (404, "{}")
      };
      var bytes = Encoding.UTF8.GetBytes(body);
      stream.Write(Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {(status == 200 ? "OK" : "Not Found")}\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n"));
      stream.Write(bytes);
   }

   (int, string) Show(string query)
   {
      lock(_askedToShow) _askedToShow.Add(query);
      return (200, """{"shown":true}""");
   }
}
