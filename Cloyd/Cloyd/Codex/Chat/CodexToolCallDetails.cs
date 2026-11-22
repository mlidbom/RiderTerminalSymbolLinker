using System.Text.Encodings.Web;
using System.Text.Json;
using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;

namespace Cloyd.Codex.Chat;

/// <summary>The input, output and outcome inside an expanded Codex tool call. Codex supplies commands and
/// their output separately, even when both span multiple lines.</summary>
sealed record CodexToolCallDetails(string? Input = null, string? Output = null, string? Patch = null, string? Outcome = null, bool Failed = false)
{
   static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

   public static CodexToolCallDetails Of(CodexChatItem item)
   {
      if(item.Details is not {} details) return new(Output: item.Text);
      var failed = details.ExitCode is not (null or 0) || details.Status is "failed" or "declined" || details.Error is { ValueKind: not JsonValueKind.Null };
      var outcome = details.ExitCode is {} exit ? $"Exit code {exit}" : details.Status switch
      {
         "completed" => null,
         "inProgress" => "Running",
         "failed" => "Failed",
         "declined" => "Declined",
         var other => other
      };
      return item.Kind switch
      {
         "commandExecution" => new(details.Command, details.Output, Outcome: outcome, Failed: failed),
         "fileChange" when details.Changes is {Count: > 0} files => new(Patch: string.Join("\n\n", files.Select(FilePatch)), Outcome: outcome, Failed: failed),
         "mcpToolCall" or "dynamicToolCall" or "functionCallOutput" => new(Formatted(details.Arguments), ToolOutput(details.Error ?? details.Result), Outcome: outcome, Failed: failed),
         _ => new(Output: item.Text, Outcome: outcome, Failed: failed)
      };
   }

   static string FilePatch(CodexChatFileChange change) =>
      $"{change.Kind} {change.Path}{(change.MovePath is null ? "" : $" → {change.MovePath}")}\n{change.Diff}";

   static string? ToolOutput(JsonElement? result)
   {
      if(result is not {} value) return null;
      if(value.ValueKind == JsonValueKind.Object && value.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
      {
         var shown = ContentOf(content);
         if(!value.TryGetProperty("structuredContent", out var structured) || Formatted(structured) is not {Length: > 0} extra) return shown;
         if(shown.Length == 0) return extra;
         return shown == extra || shown == structured.GetRawText() ? shown : $"{shown}\n\n{extra}";
      }
      return value.ValueKind == JsonValueKind.Array ? ContentOf(value) : Formatted(value);
   }

   static string ContentOf(JsonElement parts) => string.Join("\n", parts.EnumerateArray().Select(ContentOfPart));

   static string? ContentOfPart(JsonElement part)
   {
      if(part.ValueKind != JsonValueKind.Object) return Formatted(part);
      if(part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String) return text.GetString();
      var kind = Text(part, "type");
      return kind switch
      {
         "resource_link" => string.Join(" · ", new[] { Text(part, "name"), Text(part, "uri") }.OfType<string>()),
         "resource" when part.TryGetProperty("resource", out var resource) && resource.ValueKind == JsonValueKind.Object =>
            string.Join("\n", new[] { Text(resource, "uri"), Text(resource, "text") ?? Text(resource, "mimeType") }.OfType<string>()),
         "image" or "audio" or "inputImage" => $"[{Text(part, "mimeType") ?? kind}]",
         _ when part.TryGetProperty("data", out _) || part.TryGetProperty("blob", out _) => $"[{kind ?? "binary"}]",
         _ => Formatted(part)
      };
   }

   static string? Text(JsonElement part, string field) =>
      part.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

   static string? Formatted(JsonElement? value) => value?.ValueKind switch
   {
      null or JsonValueKind.Null or JsonValueKind.Undefined => null,
      JsonValueKind.String => value.Value.GetString(),
      _ => JsonSerializer.Serialize(value.Value, Indented)
   };
}
