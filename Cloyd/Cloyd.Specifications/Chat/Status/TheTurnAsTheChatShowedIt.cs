using Cloyd.Chat;
using Cloyd.Sessions;

namespace Cloyd.Specifications.Chat.Status;

/// <summary>
/// What a chat's tab, <paramref name="tab"/>, showed of Claude's turn as it went, each time <see cref="LookedAt"/> looked: what its
/// working line said, and Claude's text at the conversation's end. Looked at in a condition of
/// <see cref="SpecificationOpeningWindows.WaitUntil"/>, every 10 ms, so that a specification tells what the chat showed
/// at each step of a turn that goes on, and not only at the moment its facts look.
/// </summary>
sealed class TheTurnAsTheChatShowedIt(SessionTab tab)
{
   readonly ChatOfAClaudeCodeSession _chat = tab.ClaudeCodeChat;
   readonly List<string> _workingLineSaid = [];
   readonly List<string> _claudesTextAtTheEnd = [];

   /// <summary>What the working line said, in order, once each time it changed, such as <c>Thinking… (3s)</c> and <c>Thinking… (4s)</c>.</summary>
   public IReadOnlyList<string> WorkingLineSaid => _workingLineSaid;

   /// <summary>What the working line said Claude did, its line up to the turn's time, in order, once each time it changed: such as <c>Thinking…</c>.</summary>
   public IReadOnlyList<string> WhatClaudeDid => [.. Changes(_workingLineSaid.Select(it => it[..it.IndexOf(" (", StringComparison.Ordinal)]))];

   /// <summary>
   /// Claude's text as the chat showed it at the conversation's end, before the line of the turn's end once the turn has
   /// ended, in order, once each time it changed: what Claude wrote as it came, then what the conversation has.
   /// </summary>
   public IReadOnlyList<string> ClaudesTextAtTheEnd => _claudesTextAtTheEnd;

   /// <summary>What it saw, for a wait in vain to tell: see <see cref="SpecificationOpeningWindows.WaitUntil"/>.</summary>
   public string WhatItSaw() =>
      $"the working line said {string.Join(", then ", WorkingLineSaid.Select(it => $"\"{it}\""))}; Claude's text at the end was {string.Join(", then ", ClaudesTextAtTheEnd.Select(it => $"\"{it}\""))}.";

   /// <summary>Looks at the chat, on the UI thread, and notes what changed since it looked last. Returns true, so that it goes first in a condition: <c>turn.LookedAt() &amp;&amp; …</c>.</summary>
   public bool LookedAt()
   {
      if(_chat.WorkingLine.Says is {} says) AddIfChanged(_workingLineSaid, says);
      if(_chat.Shown.LastOrDefault(it => it is not ShownInTheChat.EndOfATurn) is ShownInTheChat.ClaudesText { Markdown: var text }) AddIfChanged(_claudesTextAtTheEnd, text);
      return true;

      static void AddIfChanged(List<string> seen, string now)
      {
         if(seen.Count == 0 || seen[^1] != now) seen.Add(now);
      }
   }

   static IEnumerable<string> Changes(IEnumerable<string> said)
   {
      string? last = null;
      foreach(var it in said)
      {
         if(it != last) yield return it;
         last = it;
      }
   }
}
