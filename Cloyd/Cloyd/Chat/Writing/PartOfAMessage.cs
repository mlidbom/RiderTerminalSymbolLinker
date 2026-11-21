using System.Text.Json.Serialization;
using ClaudeTooling.Hub.LocalApi.Contract.TypingIntoRunningSessions;

namespace Cloyd.Chat.Writing;

/// <summary>
/// A part of a <see cref="MessageFromTheChat"/>, as it came into the chat's input box: text Magnus typed, text he
/// pasted, or an image he pasted. The hub hands each on to Claude Code as it came in, typed as keystrokes or pasted, so
/// that Claude Code marks as pasted only what was, as it does in its own input box.
/// </summary>
/// <remarks>
/// Saved with its window, in a draft, as of the kind the hub's <see cref="PartInTheRequest"/> names it.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(TypedText), PartInTheRequest.TheKindOfTypedText)]
[JsonDerivedType(typeof(PastedText), PartInTheRequest.TheKindOfPastedText)]
[JsonDerivedType(typeof(PastedImage), PartInTheRequest.TheKindOfAPastedImage)]
abstract partial record PartOfAMessage
{
   /// <summary>The part as the hub's <c>POST /running-sessions/{processId}/send</c> takes it.</summary>
   public abstract PartInTheRequest InTheRequest();
}
