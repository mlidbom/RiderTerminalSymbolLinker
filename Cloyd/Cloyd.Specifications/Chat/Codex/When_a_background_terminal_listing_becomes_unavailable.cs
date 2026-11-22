using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ClaudeTooling.Hub.LocalApi.Contract;
using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;
using Cloyd.Codex;
using Cloyd.Codex.Chat;
using Cloyd.Codex.Chat.Status;
using Cloyd.Hub;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Codex;

public class When_a_background_terminal_listing_becomes_unavailable : SpecificationOpeningWindows
{
   [XF] public async Task HTTP_501_means_the_daemon_does_not_supply_the_optional_list()
   {
      using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
      using var listener = new TcpListener(IPAddress.Loopback, 0);
      listener.Start();
      var folder = Directory.CreateTempSubdirectory("Cloyd-background-list-");
      var file = Path.Combine(folder.FullName, WhatTheAnnouncementSays.FileName);
      try
      {
         var announcement = new WhatTheAnnouncementSays(LocalApiProtocol.Version, ((IPEndPoint)listener.LocalEndpoint).Port, "token", Environment.ProcessId,
                                                        DateTime.UtcNow, folder.FullName, folder.FullName) { SupportsCodexChat = true };
         await File.WriteAllTextAsync(file, JsonSerializer.Serialize(announcement), deadline.Token);
         var pending = TheHub.AnnouncedIn(folder).BackgroundTerminals(new CodexSessionId(Guid.NewGuid().ToString()), deadline.Token);
         using var client = await listener.AcceptTcpClientAsync(deadline.Token);
         var stream = client.GetStream();
         using var request = new StreamReader(stream, leaveOpen: true);
         while(await request.ReadLineAsync(deadline.Token) is {Length: > 0}) {}
         await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 501 Not Implemented\r\nContent-Length: 0\r\n\r\n"), deadline.Token);
         (await pending).Must().BeNull();
      }
      finally
      {
         File.Delete(file);
         folder.Delete();
      }
   }

   [XF] public void an_unsupported_listing_is_hidden_and_not_asked_for_on_every_turn()
   {
      OnTheUIThread(() =>
      {
         var requests = 0;
         var view = new BackgroundTerminalsOfACodexChat((_, _) => { requests++; return Task.FromResult<CodexChatBackgroundTerminals?>(null); });
         var session = new CodexSessionId("session");
         view.Observe(session, null);
         view.Observe(session, new CodexTurnId("another-turn"));
         view.Observe(session, null);
         requests.Must().Be(1);
         view.IsVisible.Must().BeFalse();
         view.Observe(new CodexSessionId("next"), null);
         requests.Must().Be(2);
         view.Observe(null, null);
      });
   }

   [XF] public void a_late_reply_from_the_old_session_does_not_repopulate_the_new_footer()
   {
      OnTheUIThread(() =>
      {
         var old = new CodexSessionId("old");
         var late = new TaskCompletionSource<CodexChatBackgroundTerminals?>();
         CancellationToken firstRequest = default;
         var view = new BackgroundTerminalsOfACodexChat((id, cancellation) =>
         {
            if(id != old) return Task.FromResult<CodexChatBackgroundTerminals?>(new([]));
            firstRequest = cancellation;
            return late.Task;
         });
         view.Observe(old, new CodexTurnId("turn"));
         view.Observe(new CodexSessionId("new"), null);
         firstRequest.IsCancellationRequested.Must().BeTrue();
         late.SetResult(new([new CodexChatBackgroundTerminal("one", "terminal", "old command", ".", null)]));
         view.IsVisible.Must().BeFalse();
         view.Observe(null, null);
      });
   }
}
