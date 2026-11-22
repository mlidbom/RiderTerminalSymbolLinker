using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ClaudeTooling.Hub.LocalApi.Contract;
using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;
using Cloyd.Chat.Writing;
using Cloyd.Codex;
using Cloyd.Codex.Chat;

namespace Cloyd.Hub;

partial class TheHub : ICodexChatInTheHub
{
   /// <summary>A fresh snapshot on each connection, followed by ordered whole-turn replacements.
   /// A null reading tells the composer the connection ended, before the next hub is contacted.</summary>
   public async IAsyncEnumerable<CodexChatReading?> Follow(CodexSessionId session, [EnumeratorCancellation] CancellationToken cancellation)
   {
      await foreach(var stream in EachStreamAt($"codex-chat/{session}/stream", cancellation))
      {
         CodexConversation? conversation = null;
         await using var events = stream.Events.GetAsyncEnumerator(cancellation);
         while(await ReadNextCodexEvent(events))
         {
            var sent = events.Current;
            switch(sent.Name)
            {
               case CodexChatEvents.Snapshot:
                  var snapshot = JsonSerializer.Deserialize<CodexChatSnapshot>(sent.Data, LocalApiProtocol.Json)!;
                  if(snapshot.SessionId != session.Value) throw new InvalidDataException("The hub sent the snapshot of a different Codex session.");
                  conversation = new CodexConversation(snapshot);
                  break;
               case CodexChatEvents.Turn:
                  if(conversation is null) throw new InvalidDataException("The hub sent a Codex turn before the session snapshot.");
                  conversation.Update(JsonSerializer.Deserialize<CodexChatTurn>(sent.Data, LocalApiProtocol.Json)!);
                  break;
               default:
                  continue;
            }
            yield return conversation.AsShown();
         }
         yield return null;
      }
   }

   static async ValueTask<bool> ReadNextCodexEvent(IAsyncEnumerator<ServerSentEvent> events)
   {
      try { return await events.MoveNextAsync(); }
      catch(HttpIOException failure) when(failure.HttpRequestError == HttpRequestError.ResponseEnded) { return false; }
      catch(IOException failure) when(failure.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionReset }) { return false; }
   }

   public Task Send(CodexSessionId session, MessageFromTheChat message, CodexTurnId? steerInto)
   {
      if(message.Parts.Any(it => it is PartOfAMessage.PastedImage))
         throw new RefusedByTheHub("This hub accepts text only. Attach the image in Terminal.");
      return steerInto is null
                ? CodexChatCommand(session, "send", new SendToCodexChat(message.AsShownInTheChat))
                : CodexChatCommand(session, "steer", new SteerCodexChat(steerInto.Value, message.AsShownInTheChat), steerInto);
   }

   public Task Interrupt(CodexSessionId session, CodexTurnId turn) => CodexChatCommand(session, "stop", new StopCodexChat(turn.Value), turn);

   /// <summary>Submits exactly once, even when a hub restarts partway through its answer. Only an explicit
   /// refusal establishes that Codex did not take the command.</summary>
   async Task CodexChatCommand<TRequest>(CodexSessionId session, string command, TRequest body, CodexTurnId? expected = null)
   {
      if(AnnouncementOfTheHub.In(_folderOfTheAnnouncement) is not {} hub)
         throw new RefusedByTheHub("The hub is unavailable. The command was not sent.");
      if(hub.Protocol != LocalApiProtocol.Version || !hub.Says.SupportsCodexChat)
         throw new RefusedByTheHub("The current hub does not offer Codex Chat to this version of Cloyd.");
      using var deadline = new CancellationTokenSource(HowLongANewHubMayTakeToAnswer);
      using var answer = await SendOnce(hub, it => new HttpRequestMessage(HttpMethod.Post, it.AddressOf($"codex-chat/{session}/{command}"))
      {
         Content = JsonContent.Create(body, options: LocalApiProtocol.Json)
      }, HttpCompletionOption.ResponseContentRead, deadline.Token);
      if(answer.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound)
         throw new RefusedByTheHub((await Read<RefusalAnswer>(answer, CancellationToken.None)).Error);
      if(answer.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest)
         throw new RefusedByTheHub($"The hub rejected the {command} request (HTTP {(int)answer.StatusCode}).");
      var receipt = await Read<CodexChatCommandAccepted>(answer, deadline.Token);
      if(string.IsNullOrWhiteSpace(receipt?.TurnId) || expected is not null && receipt.TurnId != expected.Value)
         throw new InvalidDataException("The hub did not acknowledge the expected Codex turn.");
   }
}
