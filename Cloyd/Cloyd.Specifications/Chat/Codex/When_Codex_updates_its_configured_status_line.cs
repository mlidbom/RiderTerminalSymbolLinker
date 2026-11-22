using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;
using Cloyd.Chat.Status;
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
      Displayed(chat).Must().SequenceEqual(["week 11%"]);
      chat.Update(After);
      Displayed(chat).Must().SequenceEqual(["week 100%", "1.13M used", "0 out", "Context 64% used"]);
      chat.AsShown().TurnUnderWay!.Value.Must().Be("turn");
      chat.Update(new CodexChatTurn("turn", "completed", []));
      chat.AsShown().Footer.Must().Be(After);
   }

   [XF] public void only_the_configured_items_with_readings_get_a_place_in_the_line()
   {
      var changed = After with { ConfiguredItems = ["current-dir", "not-a-known-item", "total-output-tokens", "used-tokens", "weekly-limit"], UsedTokens = 0, CurrentDirectory = null };
      new CodexStatusLineItems(changed).InOrder().Select(it => it.Text).Must().SequenceEqual(["0 out", "week 100%"]);
   }

   [XF] public void weekly_usage_uses_the_same_warning_colours_and_bold_alarm_as_Claude()
   {
      Weekly(100).Must().Be(new StretchOfTheStatusLine("week 0%", null, false));
      Weekly(25).Must().Be(new StretchOfTheStatusLine("week 75%", null, false));
      Weekly(24).Must().Be(new StretchOfTheStatusLine("week 76%", "yellow", false));
      Weekly(16).Must().Be(new StretchOfTheStatusLine("week 84%", "yellow", false));
      Weekly(15).Must().Be(new StretchOfTheStatusLine("week 85%", "red", true));
      Weekly(0).Must().Be(new StretchOfTheStatusLine("week 100%", "red", true));
   }

   [XF] public void with_footer_colours_disabled_the_alarm_still_uses_bold() =>
      Weekly(15, useColours: false).Must().Be(new StretchOfTheStatusLine("week 85%", null, true));

   [XF] public void the_configured_week_stays_visible_when_a_reading_is_unavailable_and_recovers_on_update()
   {
      var chat = new CodexConversation(new CodexChatSnapshot("session", []) { Footer = Before });
      chat.Update(Before with { WeeklyRemainingPercent = null });
      Displayed(chat).Must().SequenceEqual(["week ?"]);
      chat.Update(Before with { WeeklyRemainingPercent = 100 });
      Displayed(chat).Must().SequenceEqual(["week 0%"]);
      chat.Update(Before with { ConfiguredItems = [] });
      Displayed(chat).Must().SequenceEqual([]);
   }

   static StretchOfTheStatusLine Weekly(int remaining, bool useColours = true) =>
      new CodexStatusLineItems(Before with { WeeklyRemainingPercent = remaining, UseColors = useColours }).InOrder().Single();

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
      footer.InOrder().Select(it => it.Text).Must().SequenceEqual(["GPT-6.2-Astra", "Context 64% used", "Ask for approval"]);
      footer.PermissionsLine.Must().Be("");
   }
}
