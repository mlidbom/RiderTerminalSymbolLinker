using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Cloyd.Codex;
using Cloyd.Codex.Chat;
using Cloyd.Saving;
using Cloyd.Sessions;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Codex;

/// <summary>The terminal's host is still at its shell prompt after Codex exited. The project reopens
/// the tab, which can read its saved conversation without resuming a turn or enabling Send.</summary>
public class When_Chat_opens_for_a_saved_Codex_session_after_it_exited : SpecificationOpeningWindows
{
   readonly SessionTab _tab;

   public When_Chat_opens_for_a_saved_Codex_session_after_it_exited() => _tab = Once(() =>
   {
      var project = Sandbox.Project("after-codex-exited");
      var session = new CodexSession(new CodexSessionId(Guid.NewGuid().ToString()), "previous run");
      new CodexSessionOfASpecification(session.Id, project.Project.Folder, "saved prompt") { RecordedItems = RecordedTurn() }.WriteItsRecordFor(Sandbox.HubOfTheSandbox);
      var host = project.StartASessionHost(project.LabelOfCloydsHosts);
      project.SaveTheWindowAs(new SavedProjectWindow([new SavedSessionTab(host.Id, project.Project.Folder, session)], host.Id, null));
      var window = Open(project.Window);
      WaitUntil(() => window.BringingBackTheTabs.IsCompleted, "the tab reopened");
      var tab = OnTheUIThread(() => window.Tabs.Single());
      WaitUntil(() => tab.CodexChat?.Shown.OfType<CodexReply>().Any(it => it.Markdown == "Heard: saved prompt") == true,
                "Chat showed the saved reply", () => tab.CodexChat?.Notice ?? tab.Title);
      OpenRow("3 tool calls, 1 failed");
      OpenRow("Command");
      OpenRow("File change");
      OpenRow("Tool call");
      Render(window, "a-saved-Codex-chat-with-expanded-tools");
      return tab;

      void OpenRow(string prefix) => Click(OnTheUIThread(() => tab.CodexChat!.GetVisualDescendants().OfType<Button>().Single(it =>
         it.Content is TextBlock line && line.Inlines?.Text?.StartsWith(prefix, StringComparison.Ordinal) == true)));
   });

   [XF] public void the_session_is_still_exited() => OnTheUIThread(() => _tab.SessionRunningInIt).Must().BeNull();
   [XF] public void the_saved_conversation_cannot_take_input() => OnTheUIThread(() => _tab.CodexChat!.InputBox.CanSend).Must().BeFalse();

   [XF] public void the_multiline_command_and_its_output_are_shown_separately() =>
      SelectableText().Must().Satisfy(it => it.Contains("Get-Location\nGet-Date") && it.Contains("command output\nsecond line"));

   [XF] public void the_changed_lines_and_the_tool_result_can_be_read() =>
      SelectableText().Must().Satisfy(it => it.Contains("-before") && it.Contains("+after") && it.Any(output => output?.Contains("symbol found") == true));

   [XF] public void an_MCP_result_keeps_its_resource_links_embedded_text_and_structured_content() =>
      SelectableText().Must().Satisfy(it => it.Any(output => output is not null && output.Contains("report · file:///report") &&
                                                                output.Contains("resource contents") && output.Contains("\"count\": 31")));

   List<string?> SelectableText() => OnTheUIThread(() => _tab.CodexChat!.GetVisualDescendants().OfType<SelectableTextBlock>().Select(it => it.Text).ToList());

   static JsonArray RecordedTurn() => JsonNode.Parse("""
      [
         {"id":"prompt","type":"userMessage","content":[{"type":"text","text":"saved prompt"}]},
         {"id":"command","type":"commandExecution","command":"Get-Location\nGet-Date","aggregatedOutput":"command output\nsecond line","exitCode":1,"status":"failed"},
         {"id":"patch","type":"fileChange","status":"completed","changes":[{"path":"settings.txt","diff":"@@ -1 +1 @@\n-before\n+after","kind":{"type":"update"}}]},
         {"id":"tool","type":"mcpToolCall","server":"rider","tool":"find","status":"completed","arguments":{"name":"Example"},
          "result":{"content":[{"type":"text","text":"symbol found"},{"type":"resource_link","name":"report","uri":"file:///report"},
                               {"type":"resource","resource":{"uri":"file:///note","text":"resource contents"}}],"structuredContent":{"count":31}}},
         {"id":"answer","type":"agentMessage","text":"Heard: saved prompt","phase":"final_answer"}
      ]
      """)!.AsArray();
}
