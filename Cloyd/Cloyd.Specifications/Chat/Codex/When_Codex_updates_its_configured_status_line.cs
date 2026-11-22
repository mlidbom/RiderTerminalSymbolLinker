using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;
using Cloyd.Codex.Chat;
using Cloyd.Codex.Chat.Status;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Codex;

/// <summary>A usage report, a settings change or a new snapshot can replace the footer without adding
/// conversation items. An unavailable reading differs from a measured zero.</summary>
public class When_Codex_updates_its_configured_status_line
{
   static readonly CodexChatFooter Before = new("session", ["weekly-limit", "used-tokens", "total-output-tokens", "context-used"], true) { WeeklyRemainingPercent = 89 };
   static readonly CodexChatFooter After = Before with { UsedTokens = 1_130_000, WeeklyRemainingPercent = 0, TotalOutputTokens = 0, ContextUsedPercent = 64 };

   [XF] public void a_footer_only_update_preserves_the_turn_and_shows_known_values_in_order()
   {
      var chat = new CodexConversation(new CodexChatSnapshot("session", [new CodexChatTurn("turn", "inProgress", [])]) { Footer = Before });
      Displayed(chat).Must().SequenceEqual(["weekly 89% left"]);
      chat.Update(After);
      Displayed(chat).Must().SequenceEqual(["weekly 0% left", "1.13M used", "0 out", "Context 64% used"]);
      chat.AsShown().TurnUnderWay!.Value.Must().Be("turn");
      chat.Update(new CodexChatTurn("turn", "completed", []));
      chat.AsShown().Footer.Must().Be(After);
   }

   [XF] public void only_the_configured_items_with_readings_get_a_place_in_the_line()
   {
      var changed = After with { ConfiguredItems = ["current-dir", "not-a-known-item", "total-output-tokens", "used-tokens", "weekly-limit"], UsedTokens = 0, CurrentDirectory = null };
      new CodexStatusLineItems(changed).InOrder().Select(it => it.Text).Must().SequenceEqual(["0 out", "weekly 0% left"]);
   }

   [XF] public void permissions_already_in_the_configured_line_are_not_repeated_in_the_line_below()
   {
      var footer = new CodexStatusLineItems(After with { ConfiguredItems = ["approval-mode", "permissions"], PermissionProfile = ":danger-full-access", ApprovalPolicy = "on-request", ApprovalsReviewer = "autoReview" });
      footer.InOrder().Select(it => it.Text).Must().SequenceEqual(["Approve for me", "Full Access"]);
      footer.PermissionsLine.Must().Be("");
   }

   static IEnumerable<string> Displayed(CodexConversation chat) => new CodexStatusLineItems(chat.AsShown().Footer!).InOrder().Select(it => it.Text);

   [XF] public void native_item_names_and_their_older_spellings_use_the_same_readings()
   {
      var footer = new CodexStatusLineItems(After with { ConfiguredItems = ["session-id", "model", "context-usage", "approval"], Model = "GPT-6.2-Astra", ApprovalPolicy = "on-request", ApprovalsReviewer = "user" });
      footer.InOrder().Select(it => it.Text).Must().SequenceEqual(["session", "GPT-6.2-Astra", "Context 64% used", "Ask for approval"]);
      footer.PermissionsLine.Must().Be("");
   }
}
