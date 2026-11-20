using System.Text.Json;
using System.Text.RegularExpressions;
using ClaudeTooling.Hub.LocalApi.Contract.Conversations;
using ClaudeTooling.Hub.LocalApi.Contract.Conversations.Messages;

namespace Cloyd.Chat.ToolCalls;

/// <summary>
/// A tool Claude called, as the chat shows it: a row of a run of calls, its verb grey and what it acted on dark, such as
/// <c>Read README.md</c>, <c>Ran ls...</c> or <c>Reading NOTES.md Failed</c>; opened, what the tool was given and gave back.
/// </summary>
/// <param name="Call">The call, as the hub's conversation has it.</param>
/// <param name="Result">Its result: null until it comes.</param>
/// <remarks>
/// Equal to another of the same call whose result is the same, read from any reading of the hub's stream.
/// </remarks>
sealed partial record ToolCallInTheChat(MessageInTheAnswer.ToolCall Call, ToolResultInTheAnswer? Result)
{
   /// <summary>How long what a command's row shows of it may be before it is cut, with <c>...</c> after it.</summary>
   const int LongestCommandShown = 60;

   /// <summary>What Claude Code's result of a call Magnus denied, or interrupted while it waited to run, begins with.</summary>
   const string BeginningOfAStoppedCallsResult = "The user doesn't want to proceed with this tool use.";

   /// <summary>What a Write's result begins with when the file was new.</summary>
   const string BeginningOfACreatedFilesResult = "File created successfully";

   /// <summary>
   /// The tools whose calls the chat doesn't show among the others: Claude Code's task list, <c>TodoWrite</c>, which the app
   /// shows in a box of its own while Claude works; and the app's own tools for its Code sessions, which its plan and recap
   /// stand for.
   /// </summary>
   static readonly string[] ToolsNotShownAsCalls = ["TodoWrite"];

   const string PrefixOfTheDesktopAppsOwnTools = "mcp__ccd_turn__";

   /// <summary>Whether the chat shows <paramref name="call"/> as a call: see <see cref="ToolsNotShownAsCalls"/>.</summary>
   public static bool IsShown(MessageInTheAnswer.ToolCall call) =>
      !ToolsNotShownAsCalls.Contains(call.Name) && !call.Name.StartsWith(PrefixOfTheDesktopAppsOwnTools, StringComparison.Ordinal);

   /// <summary>The call's message's id.</summary>
   public string Id => Call.Id;

   public OutcomeOfAToolCall Outcome => Result switch
   {
      null => OutcomeOfAToolCall.Running,
      { IsError: false } => OutcomeOfAToolCall.Done,
      _ when Result.Text.StartsWith(BeginningOfAStoppedCallsResult, StringComparison.Ordinal) => OutcomeOfAToolCall.Stopped,
      _ => OutcomeOfAToolCall.Failed
   };

   public KindOfToolCall Kind => Call.Name switch
   {
      "Read" => KindOfToolCall.Reading,
      "Edit" or "MultiEdit" or "NotebookEdit" => KindOfToolCall.Editing,
      "Write" when Result is { IsError: false } && !Result.Text.StartsWith(BeginningOfACreatedFilesResult, StringComparison.Ordinal) => KindOfToolCall.Overwriting,
      "Write" => KindOfToolCall.Creating,
      "Bash" or "PowerShell" => KindOfToolCall.Running,
      "Grep" or "Glob" => KindOfToolCall.Searching,
      "WebFetch" => KindOfToolCall.Fetching,
      "WebSearch" => KindOfToolCall.SearchingTheWeb,
      "Agent" or "Task" => KindOfToolCall.Delegating,
      _ => KindOfToolCall.Calling
   };

   /// <summary>The row's verb: in the past tense once done, else in the present tense.</summary>
   public string Verb => Outcome == OutcomeOfAToolCall.Done ? Kind.PastTense : Kind.PresentTense;

   /// <summary>
   /// What the call acted on, as its row shows it: a file's name, the description Claude gave a command or the command cut
   /// short, a pattern, a page, a task's description; for another tool, its name.
   /// </summary>
   public string Target => Call.Name switch
   {
      "Read" or "Edit" or "MultiEdit" or "Write" => Path.GetFileName(Text("file_path")),
      "NotebookEdit" => Path.GetFileName(Text("notebook_path")),
      "Bash" or "PowerShell" => Text("description") is {Length: > 0} description ? description : FirstCommandOf(Text("command")),
      "Grep" or "Glob" => Text("pattern"),
      "WebFetch" => Text("url"),
      "WebSearch" => Text("query"),
      "Agent" or "Task" => Text("description"),
      _ => NameOfTheTool
   };

   /// <summary>The command a <c>Bash</c> or <c>PowerShell</c> call ran: null for a call of another tool.</summary>
   public string? Command => Call.Name is "Bash" or "PowerShell" ? Text("command") : null;

   /// <summary>The change found as <see cref="Change"/>, once it has been asked for.</summary>
   ChangeToAFile? _change;

   /// <summary>
   /// The change the call made to a file, as an edit or a write gave it: null for a call of another tool. Found once, as
   /// it is first asked for: finding it compares the lines before with the lines after, each with each.
   /// </summary>
   public ChangeToAFile? Change => _change ??= ChangeItMade();

   ChangeToAFile? ChangeItMade() => Call.Name switch
   {
      "Edit" => ChangeToAFile.From(Text("old_string"), Text("new_string")),
      "MultiEdit" when Call.Input.TryGetProperty("edits", out var edits) =>
         ChangeToAFile.From(string.Join("\n", edits.EnumerateArray().Select(it => TextIn(it, "old_string"))),
                            string.Join("\n", edits.EnumerateArray().Select(it => TextIn(it, "new_string")))),
      "Write" => ChangeToAFile.Creating(Text("content")),
      _ => null
   };

   /// <summary>How many lines the call's change to a file put in: 0 for a call that changed none.</summary>
   public int LinesAdded => Change?.LinesAdded ?? 0;

   public bool Equals(ToolCallInTheChat? other) =>
      other is not null && Id == other.Id && Result?.Time == other.Result?.Time && Result?.IsError == other.Result?.IsError && Result?.Characters == other.Result?.Characters;

   public override int GetHashCode() => HashCode.Combine(Id, Result?.Time);

   /// <summary>The tool's name as a row shows it: an MCP tool's without the <c>mcp__server__</c> before it.</summary>
   string NameOfTheTool => Call.Name.StartsWith("mcp__", StringComparison.Ordinal) ? Call.Name.Split("__", 3)[^1] : Call.Name;

   string Text(string field) => TextIn(Call.Input, field);

   static string TextIn(JsonElement input, string field) =>
      input.ValueKind == JsonValueKind.Object && input.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";

   /// <summary>
   /// The first command <paramref name="commandLine"/> runs, after any <c>cd</c> into the folder it runs in, with
   /// <c>...</c> after it when more follows, as the app shows a command Claude gave no description: <c>ls...</c>.
   /// </summary>
   static string FirstCommandOf(string commandLine)
   {
      var afterTheCd = CdBeforeTheCommand().Replace(commandLine, "", 1);
      var first = EndOfTheFirstCommand().Split(afterTheCd, 2)[0].TrimEnd();
      if(first.Length > LongestCommandShown) first = first[..LongestCommandShown];
      return first.Length < afterTheCd.TrimEnd().Length ? first + "..." : first;
   }

   [GeneratedRegex("""^\s*cd\s+("[^"]*"|'[^']*'|\S+)\s*(&&|;)\s*""")]
   private static partial Regex CdBeforeTheCommand();

   [GeneratedRegex(@"&&|\|\||[;|\n]")]
   private static partial Regex EndOfTheFirstCommand();
}
