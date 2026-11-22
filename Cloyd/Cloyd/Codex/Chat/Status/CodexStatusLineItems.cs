using System.Globalization;
using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;
using Cloyd.Chat.Status;

namespace Cloyd.Codex.Chat.Status;

/// <summary>Codex's configured status items in their chosen order, with the weekly usage styled as in Claude Chat.
/// The three token counters retain the distinctions and compact units of the Codex terminal.</summary>
sealed class CodexStatusLineItems(CodexChatFooter footer)
{
   readonly CodexChatFooter _footer = footer;

   public IEnumerable<StretchOfTheStatusLine> InOrder()
   {
      foreach(var id in _footer.ConfiguredItems)
      {
         if(ValueOf(id) is {} stretch) yield return stretch;
      }
   }

   public string PermissionsLine => string.Join(" · ", new[]
   {
      _footer.ConfiguredItems.Contains("permissions") ? null : PermissionProfile,
      _footer.ConfiguredItems.Any(it => it is "approval-mode" or "approval") ? null : ApprovalMode
   }.OfType<string>());

   StretchOfTheStatusLine? ValueOf(string id) => id switch
   {
      "model" or "model-name" => Item(_footer.Model, "yellow"),
      "model-with-reasoning" => Item(_footer.Model is null ? null : string.Join(" ", new[] { _footer.Model, _footer.ReasoningEffort, _footer.ServiceTier }.Where(it => !string.IsNullOrWhiteSpace(it))), "yellow"),
      "reasoning" => Item(_footer.ReasoningEffort, "yellow"),
      "current-dir" => Item(DirectoryAsDisplayed(_footer.CurrentDirectory), "green"),
      "context-used" or "context-usage" when _footer.ContextUsedPercent is {} used => Item($"Context {used}% used", "yellow"),
      "context-remaining" when _footer.ContextUsedPercent is {} used => Item($"Context {100 - used}% left", "yellow"),
      "weekly-limit" => WeeklyUsage(),
      "used-tokens" when _footer.UsedTokens is {} used && used > 0 => Item($"{Compact(used)} used", "yellow"),
      "total-input-tokens" when _footer.TotalInputTokens is {} input => Item($"{Compact(input)} in", "yellow"),
      "total-output-tokens" when _footer.TotalOutputTokens is {} output => Item($"{Compact(output)} out", "yellow"),
      "permissions" => Item(PermissionProfile, "cyan"),
      "approval-mode" or "approval" => Item(ApprovalMode, "cyan"),
      _ => null
   };

   StretchOfTheStatusLine? Item(string? text, string colour) =>
      string.IsNullOrEmpty(text) ? null : new(text, _footer.UseColors ? colour : null, false);

   StretchOfTheStatusLine WeeklyUsage()
   {
      if(_footer.WeeklyRemainingPercent is not {} remaining) return new("week ?", null, false);
      var used = 100 - remaining;
      var (colour, isBold) = used switch
      {
         >= 85 => ("red", true),
         > 75 => ("yellow", false),
         _ => (null, false)
      };
      return new($"week {used}%", _footer.UseColors ? colour : null, isBold);
   }

   string? PermissionProfile => _footer.PermissionProfile switch
   {
      null => null,
      ":read-only" or "read-only" => "Read Only",
      ":workspace" or "workspace-write" => "Workspace",
      ":danger-full-access" or "danger-full-access" => "Full Access",
      var profile => profile
   };

   string? ApprovalMode => _footer.ApprovalPolicy switch
   {
      null => null,
      "on-request" or "onRequest" => _footer.ApprovalsReviewer switch
      {
         "auto_review" or "autoReview" => "Approve for me",
         "user" => "Ask for approval",
         _ => "Approval on request"
      },
      "never" => "Approvals never requested",
      var policy => $"Approvals: {policy}"
   };

   static string? DirectoryAsDisplayed(string? directory)
   {
      var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
      if(directory is null || home.Length == 0) return directory;
      if(directory.Equals(home, StringComparison.OrdinalIgnoreCase)) return "~";
      return directory.StartsWith(home + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? "~" + directory[home.Length..] : directory;
   }

   static string Compact(long tokens)
   {
      if(tokens < 1_000) return tokens.ToString(CultureInfo.InvariantCulture);
      var (scale, suffix) = tokens switch
      {
         >= 999_500_000_000 => (1_000_000_000_000m, "T"),
         >= 999_500_000 => (1_000_000_000m, "B"),
         >= 999_500 => (1_000_000m, "M"),
         _ => (1_000m, "K")
      };
      var scaled = tokens / scale;
      var digits = scaled < 10 ? 2 : scaled < 100 ? 1 : 0;
      return decimal.Round(scaled, digits, MidpointRounding.ToEven).ToString("0.##", CultureInfo.InvariantCulture) + suffix;
   }
}
