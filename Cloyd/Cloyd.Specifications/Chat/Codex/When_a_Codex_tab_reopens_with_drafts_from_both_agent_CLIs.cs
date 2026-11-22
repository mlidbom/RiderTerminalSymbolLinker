using Cloyd.Hub.Commands;
using Cloyd.Sessions;
using Cloyd.Specifications.Chat.Writing;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Codex;

/// <summary>Claude Code exits to the shell with a draft in its chat. Codex starts in that same host and
/// has a draft of its own when the window closes. Both drafts survive reopening the window.</summary>
public class When_a_Codex_tab_reopens_with_drafts_from_both_agent_CLIs : SpecificationWritingInAChat
{
   readonly SessionTab _reopened;

   public When_a_Codex_tab_reopens_with_drafts_from_both_agent_CLIs() => _reopened = Once(() =>
   {
      var project = Sandbox.Project("shared-host");
      var (window, tab) = AChatOfANewSessionIn(project);
      Type(window, "draft for Claude");
      SwitchWithCtrlH(window, tab);
      DoneOnTheUIThread(() => tab.Terminal.SendInputAsync("/exit\r"));
      WaitUntil(() => tab.SessionRunningInIt is null, "Claude Code exited to the shell");
      DoneOnTheUIThread(() => Sandbox.HubOfTheSandbox.Hub.StartCodexIn(tab.Host, CodexCommand.OfANewSession(null)));
      WaitUntil(() => tab.AgentCli == AgentCli.Codex, "the tab followed Codex in its host");
      SwitchWithCtrlH(window, tab);
      Type(window, "draft for Codex");
      Close(window);
      WaitUntil(() => project.SessionHost(tab.Host.Id) is { Attached: false }, "the host was ready to reattach");
      var next = Open(project.Window);
      WaitUntil(() => next.BringingBackTheTabs.IsCompleted && next.Tabs.SingleOrDefault()?.CodexChat is not null, "the project brought back its Codex tab");
      return OnTheUIThread(() => next.Tabs.Single());
   });

   [XF] public void Codexs_editor_recovers_its_draft() => OnTheUIThread(() => _reopened.CodexChat!.InputBox.Draft.Document.Text).Must().Be("draft for Codex");
   [XF] public void Claude_Codes_editor_recovers_its_own_draft() => OnTheUIThread(() => _reopened.ClaudeCodeChat.InputBox.Draft.Document.Text).Must().Be("draft for Claude");
}
