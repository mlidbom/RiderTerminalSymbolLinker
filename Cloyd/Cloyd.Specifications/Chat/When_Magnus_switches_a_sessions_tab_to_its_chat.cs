using Avalonia.Input;
using Cloyd.Chat;
using Cloyd.Chat.ToolCalls;
using Cloyd.Sessions;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat;

/// <summary>
/// A tab's switch from its terminal to its chat, after Magnus had Claude run a tool in the terminal: the chat shows the
/// conversation of the session that runs in the tab, and goes on with it as it goes on.
/// </summary>
public class When_Magnus_switches_a_sessions_tab_to_its_chat : SpecificationOpeningWindows
{
   readonly SandboxProject _project = SandboxProject.Named(nameof(When_Magnus_switches_a_sessions_tab_to_its_chat));
   readonly SessionTab _tab;

   public When_Magnus_switches_a_sessions_tab_to_its_chat()
   {
      var window = Open(_project.Window);
      DoneOnTheUIThread(() => window.StartANewSession(name: null));
      _tab = OnTheUIThread(() => window.Tabs.Single());
      WaitUntil(() => _tab.SessionRunningInIt is not null && _tab.Terminal.TextShown.Contains("New session"), "the tab's terminal showed the stand-in's start");
      Focus(_tab.Terminal);
      Type(window, "Run a tool that prints 2 lines");
      Press(window, Key.Enter, PhysicalKey.Enter, RawInputModifiers.None);
      Click(TheButtonWithTheTip(_tab, "Show the chat"));
      WaitUntil(() => Shown.OfType<ShownInTheChat.RunOfToolCalls>().Any(), "the chat showed the tool call");
      Click(TheButtonWithTheTip(_tab, "Show the terminal"));
      Focus(_tab.Terminal);
      Type(window, "Thanks");
      Press(window, Key.Enter, PhysicalKey.Enter, RawInputModifiers.None);
      Click(TheButtonWithTheTip(_tab, "Show the chat"));
      WaitUntil(() => Shown.OfType<ShownInTheChat.ClaudesText>().Any(it => it.Markdown == "Heard: Thanks"), "the chat showed Claude's answer to what was sent after switching back to the terminal");
      Render(window, "a-sessions-tab-switched-to-its-chat");
   }

   IReadOnlyList<ShownInTheChat> Shown => OnTheUIThread(() => _tab.Chat.Shown.ToList());

   [XF] public void the_chat_is_shown_and_the_terminal_not() => OnTheUIThread(() => (_tab.Chat.IsVisible, _tab.Terminal.IsVisible)).Must().Be((true, false));

   [XF] public void it_shows_the_prompts_magnus_typed_into_the_terminal() =>
      Shown.OfType<ShownInTheChat.Prompt>().Select(it => it.Text).Must().Satisfy(it => it.SequenceEqual(["Run a tool that prints 2 lines", "Thanks"]));

   [XF] public void it_shows_the_command_claude_ran_as_done() =>
      Shown.OfType<ShownInTheChat.RunOfToolCalls>().Single().Calls.Single().Must().Satisfy(it => it.Kind == KindOfToolCall.Running && it.Outcome == OutcomeOfAToolCall.Done);

   public override void Dispose()
   {
      base.Dispose();
      _project.Dispose();
   }
}
