using Avalonia.Input;
using Cloyd.Chat;
using Cloyd.Chat.ToolCalls;
using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat;

/// <summary>
/// A tab's switch from its terminal to its chat, after Magnus switched a new session's tab from its chat to its terminal,
/// and had Claude run a tool there: the chat shows the
/// conversation of the session that runs in the tab, and goes on with it as it goes on.
/// </summary>
public class When_Magnus_switches_a_sessions_tab_to_its_chat : SpecificationOpeningWindows
{
   readonly SessionTab _tab;

   public When_Magnus_switches_a_sessions_tab_to_its_chat() => _tab = Once(() =>
   {
      var project = Sandbox.Project("the-project");
      var window = Open(project.Window);
      DoneOnTheUIThread(() => window.StartANewSession(name: null));
      var tab = OnTheUIThread(() => window.Tabs.Single());
      WaitUntil(() => tab.SessionRunningInIt is not null && tab.Terminal.TextShown.Contains("New session"), "the tab's terminal showed the stand-in's start");
      Click(TheButtonWithTheTip(tab, SessionTab.TipOfTheTerminalsSwitch));
      Focus(tab.Terminal);
      Type(window, "Run a tool that prints 2 lines");
      Press(window, Key.Enter, PhysicalKey.Enter, RawInputModifiers.None);
      Click(TheButtonWithTheTip(tab, SessionTab.TipOfTheChatsSwitch));
      WaitUntil(() => ShownIn(tab).OfType<ShownInTheChat.RunOfToolCalls>().Any(), "the chat showed the tool call");
      Click(TheButtonWithTheTip(tab, SessionTab.TipOfTheTerminalsSwitch));
      Focus(tab.Terminal);
      Type(window, "Thanks");
      Press(window, Key.Enter, PhysicalKey.Enter, RawInputModifiers.None);
      Click(TheButtonWithTheTip(tab, SessionTab.TipOfTheChatsSwitch));
      WaitUntil(() => ShownIn(tab).OfType<ShownInTheChat.ClaudesText>().Any(it => it.Markdown == "Heard: Thanks"), "the chat showed Claude's answer to what was sent after switching back to the terminal");
      Render(window, "a-sessions-tab-switched-to-its-chat");
      return tab;
   });

   IReadOnlyList<ShownInTheChat> Shown => ShownIn(_tab);

   static IReadOnlyList<ShownInTheChat> ShownIn(SessionTab tab) => OnTheUIThread(() => tab.Chat.Shown.ToList());

   [XF] public void the_tab_shows_its_chat() => OnTheUIThread(() => _tab.ShowsTheChat).Must().BeTrue();

   [XF] public void it_shows_the_prompts_magnus_typed_into_the_terminal() =>
      Shown.OfType<ShownInTheChat.Prompt>().Select(it => it.Text).Must().Satisfy(it => it.SequenceEqual(["Run a tool that prints 2 lines", "Thanks"]));

   [XF] public void it_shows_the_command_claude_ran_as_done() =>
      Shown.OfType<ShownInTheChat.RunOfToolCalls>().Single().Calls.Single().Must().Satisfy(it => it.Kind == KindOfToolCall.Running && it.Outcome == OutcomeOfAToolCall.Done);
}
