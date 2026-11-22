using System.Text.Json;
using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;
using ClaudeTooling.Hub.LocalApi.Contract;
using Cloyd.Hub;
using Cloyd.Chat.Writing;
using Cloyd.Codex;
using Cloyd.Codex.Chat;
using Cloyd.Saving;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Codex;

public class When_a_Codex_turn_ends_with_accepted_steers : SpecificationOpeningWindows
{
   static readonly CodexSessionId Session = new("session");
   static readonly CodexTurnId Turn = new("turn");

   [XF] public void reopening_restores_only_the_unrecorded_copy_of_identical_text_and_images() => OnTheUIThread(() =>
   {
      var image = new FileInfo(Path.GetTempFileName());
      try
      {
         var hub = new SteerReceipts();
         var draft = new CodexSessionDraft();
         PartOfAMessage[] parts = [new PartOfAMessage.PastedText("identical "), new PartOfAMessage.PastedImage(image)];
         foreach(var _ in Enumerable.Range(0, 2))
         {
            draft.Draft.PutBackAhead(parts);
            draft.Send(hub, Session, Turn).GetAwaiter().GetResult();
         }
         var first = hub.Submissions[0].ClientUserMessageId;
         var second = hub.Submissions[1].ClientUserMessageId;
         (first != second).Must().BeTrue();
         draft.Draft.Document.Text = "newer typing";
         var saved = JsonSerializer.Serialize(draft.AsSaved(Session));
         var reopened = new CodexSessionDraft();
         reopened.Restore(JsonSerializer.Deserialize<SavedCodexDraft>(saved)!);
         var interrupted = Reading("interrupted", second);
         reopened.Observe(interrupted with { SupportsCodexSteerReceipts = false });
         reopened.Draft.Document.Text.Must().Be("newer typing");
         reopened.Observe(interrupted);
         reopened.Observe(interrupted);
         reopened.Draft.Document.Text.Must().Be("identical [Image #1]\n\nnewer typing");
         reopened.Draft.Parts.OfType<PartOfAMessage.PastedImage>().Single().File.FullName.Must().Be(image.FullName);
         reopened.Draft.Parts.OfType<PartOfAMessage.PastedText>().Single().Text.Must().Be("identical ");
         reopened.AsSaved(Session)!.PendingSteers.Must().BeNull();
         hub.Submissions.Count.Must().Be(2);
      }
      finally { image.Delete(); }
   });

   [XF] public void a_recorded_steer_is_not_restored_when_its_acknowledgement_arrives_late() => DoneOnTheUIThread(async () =>
   {
      var hold = new TaskCompletionSource();
      var hub = new SteerReceipts(hold.Task);
      var draft = new CodexSessionDraft();
      draft.Draft.Document.Text = "observed before receipt";
      var sending = draft.Send(hub, Session, Turn);
      draft.Observe(new CodexConversation(new CodexChatSnapshot(Session.Value, []) { SupportsCodexSteerReceipts = true }).AsShown());
      draft.Draft.IsEmpty.Must().BeTrue();
      draft.Observe(Reading("inProgress", hub.Submissions.Single().ClientUserMessageId));
      draft.Draft.Document.Text = "next draft";
      draft.AsSaved(Session)!.Notice.Must().BeNull();
      draft.Observe(Reading("interrupted"));
      hold.SetResult();
      await sending;
      draft.Draft.Document.Text.Must().Be("next draft");
      draft.AsSaved(Session)!.PendingSteers.Must().BeNull();
      hub.Submissions.Count.Must().Be(1);
   });

   [XF] public void an_older_hub_keeps_the_steer_in_the_box_without_a_submission() => DoneOnTheUIThread(async () =>
   {
      var announcement = Directory.CreateTempSubdirectory("cloyd-old-steer-");
      var file = Path.Combine(announcement.FullName, "local-api.json");
      try
      {
         File.WriteAllText(file, JsonSerializer.Serialize(new { protocol = LocalApiProtocol.Version, port = 1,
            token = "test", processId = Environment.ProcessId, build = DateTime.UtcNow, claudeCodeFolder = announcement.FullName,
            codexFolder = announcement.FullName, supportsCodexChat = true }));
         var draft = new CodexSessionDraft();
         draft.Draft.Document.Text = "keep with the old hub";
         await draft.Send(TheHub.AnnouncedIn(announcement), Session, Turn);
         draft.Notice!.Must().Contain("cannot confirm individual steering messages");
         draft.Draft.Document.Text.Must().Be("keep with the old hub");
         draft.AsSaved(Session)!.PendingSteers.Must().BeNull();
      }
      finally { File.Delete(file); announcement.Delete(); }
   });

   [XF] public void the_turn_can_end_before_the_steer_receipt_returns() => DoneOnTheUIThread(async () =>
   {
      var hold = new TaskCompletionSource();
      var hub = new SteerReceipts(hold.Task);
      var draft = new CodexSessionDraft();
      draft.Draft.Document.Text = "unconsumed";
      var sending = draft.Send(hub, Session, Turn);
      draft.Draft.Document.Text = "new typing";
      draft.Observe(Reading("interrupted"));
      draft.AsSaved(Session)!.Notice!.Must().Contain("back in the box");
      hold.SetResult();
      await sending;
      draft.Draft.Document.Text.Must().Be("unconsumed\n\nnew typing");
      draft.Notice!.Must().Contain("back in the box");
      hub.Submissions.Count.Must().Be(1);
   });

   static CodexChatReading Reading(string status, params string[] ids)
   {
      var items = ids.Select(id => new CodexChatItem($"item-{id}", "userMessage", "identical", null) { ClientUserMessageId = id }).ToArray();
      return new CodexConversation(new CodexChatSnapshot(Session.Value, [new CodexChatTurn(Turn.Value, status, items)])
                                   { SupportsCodexSteerReceipts = true }).AsShown();
   }

   sealed class SteerReceipts(Task? receipt = null) : ICodexChatInTheHub
   {
      readonly Task _receipt = receipt ?? Task.CompletedTask;
      public List<SubmittedCodexSteer> Submissions { get; } = [];
      public Task Steer(CodexSessionId session, SubmittedCodexSteer steer)
      {
         Submissions.Add(steer);
         return _receipt;
      }
      public Task Send(CodexSessionId session, MessageFromTheChat message) => throw new NotSupportedException();
      public Task Interrupt(CodexSessionId session, CodexTurnId turn) => throw new NotSupportedException();
      public Task<CodexChatReading> SavedConversation(CodexSessionId session, CancellationToken cancellation) => throw new NotSupportedException();
      public IAsyncEnumerable<CodexChatReading?> Follow(CodexSessionId session, CancellationToken cancellation) => throw new NotSupportedException();
   }
}
