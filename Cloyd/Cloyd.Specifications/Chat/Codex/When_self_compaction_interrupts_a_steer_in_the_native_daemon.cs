using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input;
using Cloyd.Chat.Writing;
using Cloyd.Codex;
using Cloyd.Codex.Chat;
using Cloyd.Hub;
using Cloyd.Hub.RunningSessions;
using Cloyd.Pasting;
using Cloyd.Saving;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Codex;

/// <summary>One private native Codex, driven by the tooling's codex-cloyd-steer-recovery.py trial.
/// It releases compaction after the real Chat's POST returns, then releases a second turn to consume the explicit resend.</summary>
public class When_self_compaction_interrupts_a_steer_in_the_native_daemon : SpecificationOpeningWindows
{
   const string TrialFolder = "CLOYD_PRIVATE_STEER_TRIAL";
   public static bool HasPrivateDaemon => Environment.GetEnvironmentVariable(TrialFolder) is { Length: > 0 };

   [XF(Skip = "Run with the paired private Codex and hub trial.", SkipUnless = nameof(HasPrivateDaemon))]
   public void the_interrupted_message_returns_and_the_recorded_resend_stays_out_of_the_editor()
   {
      var folder = new DirectoryInfo(Environment.GetEnvironmentVariable(TrialFolder)!);
      if(!folder.FullName.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !folder.Name.StartsWith("cxchat-", StringComparison.Ordinal))
         throw new InvalidOperationException("This trial requires its own temporary Codex home.");
      var session = new CodexSessionId(File.ReadAllText(FileIn("cloyd-session")));
      var original = File.ReadAllText(FileIn("cloyd-text"));
      var picture = new FileInfo(FileIn("cloyd-image.png"));
      const string newer = "typing after the acknowledgement";
      var drafts = OnTheUIThread(() => new CodexDraftsOfATab());
      var hub = TheHub.AnnouncedIn(new DirectoryInfo(FileIn("api")));
      var (window, chat) = OpenChat();
      WaitUntil(() => chat.InputBox.CanSend, "Chat connected to the private Codex session");
      File.WriteAllText(FileIn("cloyd-attached"), "");
      WaitUntil(() => chat.InputBox.CanSend && chat.InputBox.ShowsTheStopButton, "Cloyd observed the issuing turn");
      OnTheUIThread(() =>
      {
         chat.InputBox.TakeTheKeyboard();
         chat.InputBox.Draft.PutBackAhead([new PartOfAMessage.TypedText(original), new PartOfAMessage.PastedImage(picture)]);
      });
      var first = Submit("first");
      Type(window, newer);
      var saved = SaveAndClose();
      SignalReceipt("first", first);
      File.WriteAllText(FileIn("compact-now"), "");
      WaitUntil(() => File.Exists(FileIn("pickup-completed")), "self-compaction and pickup finished");
      Reopen(saved);
      var recovered = original + "[Image #1]\n\n" + newer;
      WaitUntil(() => chat.InputBox.Draft.Document.Text == recovered, "the unconsumed steer returned ahead of newer typing");
      OnTheUIThread(() => chat.InputBox.Draft.Parts.OfType<PartOfAMessage.PastedImage>().Single().File.FullName).Must().Be(picture.FullName);
      OnTheUIThread(() => chat.InputBox.Notice!).Must().Contain("back in the box");
      File.WriteAllText(FileIn("cloyd-restored"), "");
      WaitUntil(() => File.Exists(FileIn("resend-ready")) && chat.InputBox.ShowsTheStopButton, "a fresh active turn for an explicit resend");
      OnTheUIThread(() => chat.InputBox.TakeTheKeyboard());
      var second = Submit("second");
      saved = SaveAndClose();
      SignalReceipt("second", second);
      WaitUntil(() => File.Exists(FileIn("resend-completed")), "the explicit resend finished");
      Reopen(saved);
      WaitUntil(() => chat.InputBox.CanSend && !chat.InputBox.ShowsTheStopButton && drafts.AsSaved is null, "a recorded steer cleared its pending submission",
                () => $"CanSend {chat.InputBox.CanSend}; Stop {chat.InputBox.ShowsTheStopButton}; draft {JsonSerializer.Serialize(drafts.AsSaved)}");
      OnTheUIThread(() => chat.InputBox.Draft.IsEmpty).Must().BeTrue();

      SubmittedCodexSteer Submit(string which)
      {
         Press(window, Key.Enter, PhysicalKey.Enter, RawInputModifiers.Control);
         WaitUntil(() => chat.InputBox.CanSend && chat.InputBox.Draft.IsEmpty && drafts.AsSaved?.Single().PendingSteers?.Count == 1,
                   $"the {which} steer received the hub's acknowledgement");
         return OnTheUIThread(() => drafts.AsSaved!.Single().PendingSteers!.Single());
      }

      void SignalReceipt(string which, SubmittedCodexSteer steer)
      {
         var receipt = FileIn($"cloyd-{which}-receipt");
         File.WriteAllText(receipt + ".writing", JsonSerializer.Serialize(new { turn = steer.Turn.Value, id = steer.ClientUserMessageId }));
         File.Move(receipt + ".writing", receipt);
      }

      (Window, ChatOfACodexSession) OpenChat()
      {
         var opened = Open(() => new Window { Width = 900, Height = 650, Content = new ChatOfACodexSession(hub, new PastedImages(folder), drafts) });
         var view = OnTheUIThread(() => (ChatOfACodexSession)opened.Content!);
         OnTheUIThread(() => view.Show(session, new RunningCodex(session, "Private Codex", null, new StatusOfACodex("busy"))));
         return (opened, view);
      }

      string SaveAndClose()
      {
         var json = OnTheUIThread(() => JsonSerializer.Serialize(drafts.AsSaved));
         Close(window);
         return json;
      }

      void Reopen(string json)
      {
         drafts = OnTheUIThread(() => new CodexDraftsOfATab());
         OnTheUIThread(() => drafts.Restore(JsonSerializer.Deserialize<SavedCodexDraft[]>(json)!));
         (window, chat) = OpenChat();
      }

      string FileIn(string name) => Path.Combine(folder.FullName, name);
   }
}
