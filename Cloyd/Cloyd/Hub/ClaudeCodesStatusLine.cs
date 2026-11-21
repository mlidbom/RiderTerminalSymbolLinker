using ClaudeTooling.Hub.LocalApi.Contract.RunningSessions;

namespace Cloyd.Hub;

/// <summary>
/// What a running Claude Code last showed under its input box, as the hub keeps it with each running session: the
/// machine's status line, in its styles, and the permission mode under it.
/// </summary>
/// <param name="Text">
/// The status line as Claude Code shows it: the model, the effort level, the context used, the weekly limits, and
/// whatever else the machine's status line command shows, such as
/// "Opus 5.5 · xhigh · context 42% · week 100% · Fable 7% · €361.07 · 72% · home 1 · .claude 24".
/// </param>
/// <param name="Stretches">The status line's stretches, each in one style, in order: their texts together are <paramref name="Text"/>.</param>
/// <param name="PermissionModeShownAs">The permission mode as Claude Code shows it under the status line, such as "bypass permissions on": null until the hub has read it off the screen.</param>
sealed record ClaudeCodesStatusLine(string Text, IReadOnlyList<StretchOfTheStatusLine> Stretches, string? PermissionModeShownAs)
{
   /// <summary>The status line as the hub's answer <paramref name="statusLine"/> has it.</summary>
   public static ClaudeCodesStatusLine InThe(StatusLineInTheAnswer statusLine) =>
      new(statusLine.Text, [.. statusLine.StyledStretches.Select(StretchOfTheStatusLine.InThe)], statusLine.PermissionMode?.ShownAs);

   /// <summary>The line under the status line, as Claude Code shows the permission mode, such as "⏵⏵ bypass permissions on": null while it isn't known.</summary>
   public string? PermissionModeLine => PermissionModeShownAs is {} mode ? $"⏵⏵ {mode}" : null;
}
