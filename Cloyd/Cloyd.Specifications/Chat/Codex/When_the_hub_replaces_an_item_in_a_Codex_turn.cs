using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;
using Cloyd.Chat;
using Cloyd.Codex.Chat;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Codex;

/// <summary>The saved part of the turn already includes a command and part of its commentary. The hub
/// later supplies the same turn with completed commentary and a second tool call.</summary>
public class When_the_hub_replaces_an_item_in_a_Codex_turn
{
   static readonly CodexChatItem Prompt = new("prompt", "userMessage", "inspect the change", null);
   static readonly CodexChatItem FirstCommand = new("one", "commandExecution", "git status\nworking tree clean", null);
   static readonly CodexChatItem SecondCommand = new("two", "commandExecution", "git branch", null);
   readonly CodexConversation _conversation = new(new CodexChatSnapshot("session",
         [new CodexChatTurn("turn", "inProgress", [Prompt, FirstCommand, new CodexChatItem("text", "agentMessage", "The", "commentary")])]));

   public When_the_hub_replaces_an_item_in_a_Codex_turn() =>
      _conversation.Update(new CodexChatTurn("turn", "completed",
         [Prompt, FirstCommand, new CodexChatItem("text", "agentMessage", "The checkout is clean.", "commentary"), SecondCommand,
          new CodexChatItem("new-kind", "aFutureItemKind", "future data", null), new CodexChatItem("end", "agentMessage", "Done", "final_answer")]));

   [XF] public void the_original_order_and_completed_text_are_preserved() =>
      _conversation.AsShown().Rows.Select(it => it.TextShown).Must().SequenceEqual(["inspect the change", "Command · git status", "The checkout is clean.", "Command · git branch", "Done"]);

   [XF] public void commentary_separates_the_two_expandable_commands() =>
      _conversation.AsShown().Rows.OfType<CodexToolCalls>().Select(it => it.Calls.Single().Id).Must().SequenceEqual(["turn/one", "turn/two"]);

   [XF] public void a_finished_turn_can_no_longer_be_steered() => _conversation.AsShown().TurnUnderWay.Must().BeNull();

   [XF] public void a_prompt_attaching_the_same_image_twice_preserves_its_numbering()
   {
      const string path = @"C:\screenshots\figure.png";
      var images = new CodexChatItem("photos", "userMessage", $"{path}\n{path}", null) { Details = new CodexChatItemDetails { ImagePaths = [path, path] } };
      CodexTurnInTheChat.From(new CodexChatTurn("images", "completed", [images])).Rows.Single().TextShown.Must().Be("[Image #1]\n[Image #2]");
   }
}
