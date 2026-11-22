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
   public async Task<CodexChatBackgroundTerminals?> BackgroundTerminals(CodexSessionId session, CancellationToken cancellation)
   {
      using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
      deadline.CancelAfter(HowLongANewHubMayTakeToAnswer);
      using var answer = await Send(it => new HttpRequestMessage(HttpMethod.Get, it.AddressOf($"codex-chat/{session}/background-terminals")), deadline.Token);
      if(answer.StatusCode == HttpStatusCode.NotImplemented) return null;
      if(answer.StatusCode == HttpStatusCode.NotFound) throw new RefusedByTheHub((await Read<RefusalAnswer>(answer, deadline.Token)).Error);
      return await Read<CodexChatBackgroundTerminals>(answer, deadline.Token);
   }

   /// <summary>Reads the saved conversation without attaching a client or waking a Codex session that exited.</summary>
   public async Task<CodexChatReading> SavedConversation(CodexSessionId session, CancellationToken cancellation)
   {
      using var answer = await Send(it => new HttpRequestMessage(HttpMethod.Get, it.AddressOf($"codex-chat/{session}")), cancellation);
      if(answer.StatusCode == HttpStatusCode.NotFound) throw new RefusedByTheHub((await Read<RefusalAnswer>(answer, cancellation)).Error);
      return ConversationOf(session, await Read<CodexChatSnapshot>(answer, cancellation)).AsShown();
   }

   static CodexConversation ConversationOf(CodexSessionId session, CodexChatSnapshot snapshot) =>
      snapshot.SessionId == session.Value ? new CodexConversation(snapshot) : throw new InvalidDataException("The hub returned the conversation of a different Codex session.");

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
                  conversation = ConversationOf(session, snapshot);
                  break;
               case CodexChatEvents.Turn:
                  if(conversation is null) throw new InvalidDataException("The hub sent a Codex turn before the session snapshot.");
                  conversation.Update(JsonSerializer.Deserialize<CodexChatTurn>(sent.Data, LocalApiProtocol.Json)!);
                  break;
               case CodexChatEvents.Footer:
                  if(conversation is null) throw new InvalidDataException("The hub sent a Codex footer before the session snapshot.");
                  conversation.Update(JsonSerializer.Deserialize<CodexChatFooter>(sent.Data, LocalApiProtocol.Json)!);
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

   public Task Send(CodexSessionId session, MessageFromTheChat message) => Submit(session, message, null);
   public Task Steer(CodexSessionId session, SubmittedCodexSteer steer) => Submit(session, steer.Message, steer);

   Task Submit(CodexSessionId session, MessageFromTheChat message, SubmittedCodexSteer? steer)
   {
      var images = message.Parts.OfType<PartOfAMessage.PastedImage>().Select(it => it.File.FullName).ToArray();
      var imagePaths = images.Length == 0 ? null : images;
      var text = images.Length == message.Parts.Count ? "" : message.AsShownInTheChat;
      return steer is null
                ? CodexChatCommand(session, "send", new SendToCodexChat(text) { ImagePaths = imagePaths })
                : CodexChatCommand(session, "steer", new SteerCodexChat(steer.Turn.Value, text)
                   { ImagePaths = imagePaths, ClientUserMessageId = steer.ClientUserMessageId }, steer.Turn, steer.ClientUserMessageId);
   }

   public Task Interrupt(CodexSessionId session, CodexTurnId turn) => CodexChatCommand(session, "stop", new StopCodexChat(turn.Value), turn);

   /// <summary>Submits exactly once, even when a hub restarts partway through its answer. Only an explicit
   /// refusal establishes that Codex did not take the command.</summary>
   async Task CodexChatCommand<TRequest>(CodexSessionId session, string command, TRequest body, CodexTurnId? expected = null,
                                         string? expectedClientMessageId = null)
   {
      if(AnnouncementOfTheHub.In(_folderOfTheAnnouncement) is not {} hub)
         throw new RefusedByTheHub("The hub is unavailable. The command was not sent.");
      if(hub.Protocol != LocalApiProtocol.Version || !hub.Says.SupportsCodexChat)
         throw new RefusedByTheHub("The current hub does not offer Codex Chat to this version of Cloyd.");
      if(expectedClientMessageId is not null && !hub.Says.SupportsCodexSteerReceipts)
         throw new RefusedByTheHub("This hub cannot confirm individual steering messages. Use Terminal to steer until the hub is updated.");
      using var deadline = new CancellationTokenSource(HowLongANewHubMayTakeToAnswer);
      using var answer = await SendOnce(hub, it => new HttpRequestMessage(HttpMethod.Post, it.AddressOf($"codex-chat/{session}/{command}"))
      {
         Content = JsonContent.Create(body, options: LocalApiProtocol.Json)
      }, HttpCompletionOption.ResponseContentRead, deadline.Token);
      if(answer.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound)
         throw new RefusedByTheHub((await Read<RefusalAnswer>(answer, CancellationToken.None)).Error);
      if(answer.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest)
         throw new RefusedByTheHub($"The hub rejected the {command} request (HTTP {(int)answer.StatusCode}).");
      var turnId = await AcceptedTurn();
      if(string.IsNullOrWhiteSpace(turnId) || expected is not null && turnId != expected.Value)
         throw new InvalidDataException("The hub did not acknowledge the expected Codex turn.");

      async Task<string> AcceptedTurn()
      {
         if(expectedClientMessageId is null) return (await Read<CodexChatCommandAccepted>(answer, deadline.Token)).TurnId;
         var receipt = await Read<CodexChatSteerAccepted>(answer, deadline.Token);
         if(receipt.ClientUserMessageId != expectedClientMessageId)
            throw new InvalidDataException("The hub did not acknowledge this Codex steering message ID.");
         return receipt.TurnId;
      }
   }
}
