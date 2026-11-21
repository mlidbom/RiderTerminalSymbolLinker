namespace Cloyd.Hub;

/// <summary>
/// The block of text or thinking the model writes in a running Claude Code's turn under way, as far as it has streamed:
/// what Claude Code's own UI shows as it comes, before Claude Code writes the whole block to the transcript.
/// </summary>
/// <param name="Id">What tells it apart from the turn's other blocks: its turn's id, its response's number within the turn, and its own within the response.</param>
/// <param name="Kind"><c>text</c> or <c>thinking</c>.</param>
/// <param name="Text">What has streamed of it so far.</param>
/// <param name="HasEnded">
/// Whether it has ended: Claude Code has written it to the transcript, and the conversation's stream sends it about 0.15 s
/// later. Whoever shows it keeps showing it until the conversation has it.
/// </param>
sealed record BlockBeingWritten(string Id, string Kind, string Text, bool HasEnded)
{
   /// <summary>Whether it is text Claude writes, as the chat shows: not thinking, which the chat doesn't.</summary>
   public bool IsText => Kind == "text";
}
