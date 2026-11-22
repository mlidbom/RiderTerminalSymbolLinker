using ClaudeTooling.Hub.LocalApi.Contract.Conversations;
using ClaudeTooling.Hub.LocalApi.Contract.Conversations.Messages;
using Cloyd.Chat.ToolCalls;

namespace Cloyd.Chat;

/// <summary>
/// One of the things the chat shows of a session's conversation, one after another down its column, as the Claude desktop
/// app's Code tab shows them (<c>dev_docs/cloyd-chat-specification.md</c>): a prompt of Magnus's, what Claude wrote, a run
/// of tool calls, or a grey line telling what happened to the conversation, as a turn's end. Each kind is a type nested here.
/// </summary>
/// <remarks>
/// Each is equal to another that shows the same, whatever reading of the hub's stream either came from, so that the chat
/// redraws only what changed: after a publish of the hub, which sends the whole conversation again, nothing.
/// </remarks>
abstract partial record ShownInTheChat
{
   /// <summary>
   /// The text of the row as drawn closed. Markdown messages retain their source, including
   /// formatting and original line breaks.
   /// </summary>
   public abstract string TextShown { get; }

   /// <summary>What the chat shows of <paramref name="conversation"/>, in order.</summary>
   /// <remarks>
   /// What the app doesn't show isn't shown: Claude's thinking, an interruption, and a prompt that is a background task's
   /// notice or Claude Code's own. Nor does it end a run of tool calls: only what is shown between two calls does.
   /// </remarks>
   public static IReadOnlyList<ShownInTheChat> AllOf(ConversationAnswer conversation)
   {
      var results = conversation.ToolResults.ToDictionary(it => it.ToolUseId);
      var shown = new List<ShownInTheChat>();
      foreach(var message in conversation.Messages)
         switch(message)
         {
            case MessageInTheAnswer.PromptOfMagnuss prompt:
               shown.Add(Prompt.Of(prompt));
               break;
            case MessageInTheAnswer.SlashCommand command when SlashCommand.AskedFor(shown.LastOrDefault(), command):
               shown.Insert(shown.Count - 1, new SlashCommand(command.Id, command.AsTyped));
               break;
            case MessageInTheAnswer.SlashCommand command:
               shown.Add(new SlashCommand(command.Id, command.AsTyped));
               break;
            case MessageInTheAnswer.ClaudesText text:
               shown.Add(new ClaudesText(text.Id, text.Text));
               break;
            case MessageInTheAnswer.ToolCall call when ToolCallInTheChat.IsShown(call):
               var callShown = new ToolCallInTheChat(call, results.GetValueOrDefault(call.ToolUseId));
               if(PeerMessage.SentByClaude(callShown) is {} sent)
               {
                  shown.Add(sent);
                  break;
               }
               if(shown.LastOrDefault() is RunOfToolCalls run) shown[^1] = run.With(callShown);
               else shown.Add(new RunOfToolCalls([callShown]));
               break;
            case MessageInTheAnswer.Compaction compaction:
               shown.Add(Compaction.Of(compaction));
               break;
            case MessageInTheAnswer.Clearing clearing:
               shown.Add(new Clearing(clearing.Id));
               break;
            case MessageInTheAnswer.PromptFromElsewhere prompt when PeerMessage.FromClaudeCode(prompt) is {} peer:
               shown.Add(peer);
               break;
            case MessageInTheAnswer.PromptFromElsewhere prompt when PromptFromElsewhere.IsShown(prompt):
               shown.Add(PromptFromElsewhere.Of(prompt));
               break;
            case MessageInTheAnswer.EndOfATurn end:
               shown.Add(EndOfATurn.Of(end));
               break;
         }

      return shown;
   }
}
