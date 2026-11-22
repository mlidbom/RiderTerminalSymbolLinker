namespace Cloyd.Chat.Searching;

/// <summary>A match of a search of the chat: in the thing the chat shows at <paramref name="Shown"/>, the match at <paramref name="Occurrence"/> in its text.</summary>
/// <param name="Shown">Where in what the chat shows the thing is, as <see cref="SessionChat.Shown"/> has it.</param>
/// <param name="Occurrence">Which of the matches in the thing's text it is, from the first, at 0.</param>
readonly record struct MatchInTheChat(int Shown, int Occurrence);
