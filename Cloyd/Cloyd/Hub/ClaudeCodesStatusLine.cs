using ClaudeTooling.Hub.LocalApi.Contract.RunningSessions;

namespace Cloyd.Hub;

/// <summary>
/// What a running Claude Code last showed under its input box, as the hub keeps it with each running session: its model,
/// its effort level, how full its context window is, and its permission mode.
/// </summary>
/// <param name="Model">The model, as Claude Code names it, such as "Opus 5.5".</param>
/// <param name="EffortLevel">The effort level, such as <c>xhigh</c>: null for a model with none.</param>
/// <param name="ContextUsedPercentage">How much of the context window is used, in percent: null before the session's first reply.</param>
/// <param name="PermissionMode">The permission mode's name in Claude Code's settings, such as <c>auto</c>: null until the hub has read it off the screen, or for one it doesn't know.</param>
/// <param name="PermissionModeShownAs">The permission mode as Claude Code shows it, such as "auto mode on": null until the hub has read it off the screen.</param>
sealed record ClaudeCodesStatusLine(string Model, string? EffortLevel, double? ContextUsedPercentage, string? PermissionMode, string? PermissionModeShownAs)
{
   /// <summary>The status line as the hub's answer <paramref name="statusLine"/> has it.</summary>
   public static ClaudeCodesStatusLine InThe(StatusLineInTheAnswer statusLine) =>
      new(statusLine.Model.DisplayName, statusLine.EffortLevel, statusLine.ContextWindow.UsedPercentage, statusLine.PermissionMode?.Name, statusLine.PermissionMode?.ShownAs);

   /// <summary>
   /// The permission mode in short, as the desktop app names it beside its input box, such as "Auto": the words Claude
   /// Code shows for a mode the hub doesn't know. Null while it isn't known.
   /// </summary>
   public string? PermissionModeInShort => PermissionMode switch
   {
      "bypassPermissions" => "Bypass permissions",
      "auto" => "Auto",
      "default" => "Manual",
      "acceptEdits" => "Accept edits",
      "plan" => "Plan",
      _ => PermissionModeShownAs
   };

   /// <summary>The line as the chat shows it under its input box, such as "Opus 5.5 · xhigh · Auto · 23% context": what isn't known left out.</summary>
   public string AsShown => string.Join(" · ", new[] { Model, EffortLevel, PermissionModeInShort, ContextUsedPercentage is {} used ? $"{used:0}% context" : null }.OfType<string>());
}
