using System.Globalization;
using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;

namespace Cloyd.Codex.Chat.Status;

/// <summary>Codex's configured status items in their chosen order. Missing readings occupy no space.
/// The three token counters retain the distinctions and compact units of the Codex terminal.</summary>
sealed class CodexStatusLineItems(CodexChatFooter footer)
{
   readonly CodexChatFooter _footer = footer;

   public IEnumerable<(string Text, string Colour)> InOrder()
   {
      foreach(var id in _footer.ConfiguredItems)
      {
         var (text, colour) = ValueOf(id);
         if(!string.IsNullOrEmpty(text)) yield return (text, colour);
      }
   }

   public string PermissionsLine => string.Join(" · ", new[]
   {
      _footer.ConfiguredItems.Contains("permissions") ? null : PermissionProfile,
      _footer.ConfiguredItems.Any(it => it is "approval-mode" or "approval") ? null : ApprovalMode
   }.OfType<string>());

   (string? Text, string Colour) ValueOf(string id) => id switch
   {
      "thread-id" or "session-id" => (_footer.SessionId, "blue"),
      "model" or "model-name" => (_footer.Model, "yellow"),
      "model-with-reasoning" => (_footer.Model is null ? null : string.Join(" ", new[] { _footer.Model, _footer.ReasoningEffort, _footer.ServiceTier }.Where(it => !string.IsNullOrWhiteSpace(it))), "yellow"),
      "reasoning" => (_footer.ReasoningEffort, "yellow"),
      "current-dir" => (DirectoryAsDisplayed(_footer.CurrentDirectory), "green"),
      "thread-name" => (_footer.ThreadName, "cyan"),
      "context-used" or "context-usage" when _footer.ContextUsedPercent is {} used => ($"Context {used}% used", "yellow"),
      "context-remaining" when _footer.ContextUsedPercent is {} used => ($"Context {100 - used}% left", "yellow"),
      "weekly-limit" when _footer.WeeklyRemainingPercent is {} left => ($"weekly {left}% left", "magenta"),
      "used-tokens" when _footer.UsedTokens is {} used && used > 0 => ($"{Compact(used)} used", "yellow"),
      "total-input-tokens" when _footer.TotalInputTokens is {} input => ($"{Compact(input)} in", "yellow"),
      "total-output-tokens" when _footer.TotalOutputTokens is {} output => ($"{Compact(output)} out", "yellow"),
      "permissions" => (PermissionProfile, "cyan"),
      "approval-mode" or "approval" => (ApprovalMode, "cyan"),
      _ => (null, "white")
   };

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
