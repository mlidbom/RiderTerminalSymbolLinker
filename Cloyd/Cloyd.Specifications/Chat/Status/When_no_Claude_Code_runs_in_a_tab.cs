using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Cloyd.Chat.Status;
using Cloyd.Codex;
using Cloyd.Hub.Commands;
using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Status;

/// <summary>A project window adopts a plain shell whose host has no saved session. Its chat reports that no Claude Code runs there.</summary>
public class When_no_Claude_Code_runs_in_a_tab : SpecificationOpeningWindows
{
   readonly SessionTab _tab;

   public When_no_Claude_Code_runs_in_a_tab() => _tab = Once(() =>
   {
      var project = Sandbox.Project("the-project");
      project.StartASessionHost(project.LabelOfCloydsHosts);
      var window = Open(project.Window);
      WaitUntil(() => window.BringingBackTheTabs.IsCompleted, "the window had brought back its tabs");
      return OnTheUIThread(() => window.Tabs.Single());
   });

   IndicatorOfASessionTab Indicator => ((Control)_tab.Header!).FindDescendantOfType<IndicatorOfASessionTab>()!;

   [XF] public void the_chats_status_line_tells_so() => OnTheUIThread(() => _tab.ClaudeCodeChat.StatusLine.ClaudeCodesStatusLine).Must().Be(StatusLineOfTheChat.NoClaudeCodeRuns);

   [XF] public void the_header_leaves_the_agent_indicator_empty()
   {
      //todo:compze: Once Compze.Must bounds the size of objects it serializes for a failure, assert on Indicator.Child
      //   directly. Its unexpected SpinnerOfClaudeCode exhausted memory while BeNull formatted the attached control.
      OnTheUIThread(() => Indicator.Child?.GetType().Name).Must().BeNull();
   }

   [XF] public void the_tab_uses_its_existing_Claude_Code_fallback() => OnTheUIThread(() => _tab.AgentCli).Must().ReferenceEqual(AgentCli.ClaudeCode);

   public class and_Codex_starts_in_that_shell : When_no_Claude_Code_runs_in_a_tab
   {
      readonly Size _emptySlot;

      public and_Codex_starts_in_that_shell() => _emptySlot = Once(() =>
      {
         var size = OnTheUIThread(() => Indicator.Bounds.Size);
         DoneOnTheUIThread(() => Sandbox.HubOfTheSandbox.Hub.StartCodexIn(_tab.Host, CodexCommand.OfANewSession(name: null)));
         WaitUntil(() => _tab.SessionRunningInIt?.Session is CodexSession, "the hub identified Codex in the adopted shell");
         return size;
      });

      [XF] public void the_header_shows_the_Codex_dot() => OnTheUIThread(() => Indicator.Child is CodexProgressDot { IsEffectivelyVisible: true }).Must().BeTrue();

      [XF] public void the_dot_fits_the_space_the_header_reserved() => OnTheUIThread(() => Indicator.Bounds.Size).Must().Be(_emptySlot);
   }
}
