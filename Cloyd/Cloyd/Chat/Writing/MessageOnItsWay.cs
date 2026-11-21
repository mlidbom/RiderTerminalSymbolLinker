using Cloyd.ClaudeCode;

namespace Cloyd.Chat.Writing;

/// <summary>
/// A message Magnus sent from the chat that the conversation doesn't have yet: waiting for its turn to be given to Claude
/// Code, being given, or queued by Claude Code, which gives it to the model once it can, as when the tool under way is
/// done. The input box shows it over itself, dimmed, until the conversation has it, as Claude Code's own UI shows a
/// queued message over its box: so that a message sent while Claude works doesn't seem to vanish.
/// </summary>
sealed class MessageOnItsWay
{
   /// <summary>What the input box shows under a message being given, or waiting to be.</summary>
   public const string Sending = "Sending…";

   /// <summary>What the input box shows under a message Claude Code queued, as its own UI does.</summary>
   public const string Queued = "Queued";

   /// <summary>How many prompts of the conversation held its text as Magnus sent it: once more do, the conversation has it.</summary>
   readonly int _promptsWithItBefore;

   /// <summary>
   /// <paramref name="message"/>, sent to <paramref name="claudeCode"/> as the chat showed <paramref name="conversation"/>:
   /// on its way until the conversation has one more prompt with its text.
   /// </summary>
   public MessageOnItsWay(MessageFromTheChat message, ClaudeCodeProcessId claudeCode, IReadOnlyList<ShownInTheChat> conversation)
   {
      Message = message;
      ClaudeCode = claudeCode;
      _promptsWithItBefore = PromptsWithItIn(conversation);
   }

   public MessageFromTheChat Message { get; }

   /// <summary>The Claude Code it is given to: the one that ran in the tab as Magnus sent it.</summary>
   public ClaudeCodeProcessId ClaudeCode { get; }

   /// <summary>Whether Claude Code has taken it: queued, or given to the model at once.</summary>
   public bool IsGiven { get; private set; }

   /// <summary>Whether Claude Code queued it, to give it to the model once it can.</summary>
   public bool IsQueued { get; private set; }

   /// <summary>What the input box shows under it: <see cref="Queued"/> once Claude Code queued it, else <see cref="Sending"/>.</summary>
   public string Stage => IsQueued ? Queued : Sending;

   /// <summary>Claude Code has taken it: <paramref name="queued"/>, or given to the model at once.</summary>
   public void WasGiven(bool queued)
   {
      IsGiven = true;
      IsQueued = queued;
   }

   /// <summary>
   /// Whether <paramref name="conversation"/> has it: one more prompt holds its text than as Magnus sent it. Held in, not
   /// equal to, as Claude Code may give the model several queued messages as one prompt.
   /// </summary>
   public bool IsIn(IReadOnlyList<ShownInTheChat> conversation) => PromptsWithItIn(conversation) > _promptsWithItBefore;

   int PromptsWithItIn(IReadOnlyList<ShownInTheChat> conversation)
   {
      var text = Normalized(Message.AsShownInTheChat);
      return conversation.Count(it => it switch
      {
         ShownInTheChat.Prompt prompt => Normalized(prompt.Text).Contains(text, StringComparison.Ordinal),
         ShownInTheChat.SlashCommand command => Normalized(command.AsTyped) == text,
         _ => false
      });
   }

   static string Normalized(string text) => text.ReplaceLineEndings("\n").Trim();
}
