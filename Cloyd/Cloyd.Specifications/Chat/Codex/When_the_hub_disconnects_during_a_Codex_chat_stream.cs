using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ClaudeTooling.Hub.LocalApi.Contract;
using Cloyd.Codex;
using Cloyd.Hub;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Codex;

/// <summary>A hub dies while the client is reading a partial SSE event. The loss of the socket revokes the
/// command target, and the snapshot on the replacement connection supplies the finished message.</summary>
public class When_the_hub_disconnects_during_a_Codex_chat_stream
{
   [XF] public async Task a_reset_socket_reconnects_and_the_new_snapshot_recovers_the_reply()
   {
      using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
      using var listener = new TcpListener(IPAddress.Loopback, 0);
      listener.Start();
      var folder = Directory.CreateTempSubdirectory("Cloyd-Codex-stream-");
      var announcementFile = Path.Combine(folder.FullName, WhatTheAnnouncementSays.FileName);
      try
      {
         var announcement = new WhatTheAnnouncementSays(LocalApiProtocol.Version, ((IPEndPoint)listener.LocalEndpoint).Port, "token", Environment.ProcessId,
                                                        DateTime.UtcNow, folder.FullName, folder.FullName) { SupportsCodexChat = true };
         await File.WriteAllTextAsync(announcementFile, JsonSerializer.Serialize(announcement), deadline.Token);
         var session = new CodexSessionId(Guid.NewGuid().ToString());
         await using var readings = TheHub.AnnouncedIn(folder).Follow(session, deadline.Token).GetAsyncEnumerator(deadline.Token);

         var first = readings.MoveNextAsync().AsTask();
         using(var connection = await ConnectAndSendSnapshot("The re", "inProgress"))
         {
            (await first).Must().BeTrue();
            readings.Current!.Rows.Single().TextShown.Must().Be("The re");
            var lost = readings.MoveNextAsync().AsTask();
            connection.Client.LingerState = new LingerOption(true, 0);
            connection.Close();
            (await lost).Must().BeTrue();
            readings.Current.Must().BeNull();
         }

         var next = readings.MoveNextAsync().AsTask();
         using var replacement = await ConnectAndSendSnapshot("The result is ready.", "completed");
         (await next).Must().BeTrue();
         readings.Current!.Rows.Single().TextShown.Must().Be("The result is ready.");
         readings.Current.TurnUnderWay.Must().BeNull();

         async Task<TcpClient> ConnectAndSendSnapshot(string text, string status)
         {
            var client = await listener.AcceptTcpClientAsync(deadline.Token);
            var stream = client.GetStream();
            using var request = new StreamReader(stream, leaveOpen: true);
            while(await request.ReadLineAsync(deadline.Token) is {Length: > 0}) {}
            var snapshot = new {sessionId = session.Value, turns = new[] { new {id = "turn", status, items = new[] { new { id = "message", kind = "agentMessage", text, phase = "final_answer" } } } }};
            var headers = "HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nContent-Length: 1000000\r\n\r\n";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(headers + $"event: snapshot\ndata: {JsonSerializer.Serialize(snapshot)}\n\nevent: turn\n"), deadline.Token);
            return client;
         }
      }
      finally
      {
         File.Delete(announcementFile);
         folder.Delete();
      }
   }
}
