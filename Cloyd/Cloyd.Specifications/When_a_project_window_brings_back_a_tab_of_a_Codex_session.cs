using Avalonia.Input;
using Cloyd.Codex;
using Cloyd.Projects;
using Cloyd.Saving;
using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

/// <summary>
/// A project window opens with a saved tab whose session was Codex's, its host still running, at its shell's prompt, as
/// after Codex exits to it. An older hub announces no Codex Chat capability, so this tab offers only Terminal.
/// </summary>
public class When_a_project_window_brings_back_a_tab_of_a_Codex_session : SpecificationOpeningWindows
{
   static readonly CodexSession TheSession = new(new CodexSessionId("01a1fe6b-c285-7962-a6ae-85f69a3a51f3"), "Porting the parser");

   readonly ProjectWindow _window;
   readonly SessionTab _tab;

   public When_a_project_window_brings_back_a_tab_of_a_Codex_session() => (_window, _tab) = Once(() =>
   {
      var project = Sandbox.Project("the-project");
      Sandbox.HubOfTheSandbox.AnnounceWithoutCodexChat();
      var host = project.StartASessionHost(project.LabelOfCloydsHosts);
      project.SaveTheWindowAs(new SavedProjectWindow([new SavedSessionTab(host.Id, project.Project.Folder, TheSession)], host.Id, Desktop: null));
      var window = Open(project.Window);
      WaitUntil(() => window.BringingBackTheTabs.IsCompleted, "the window had brought back its tabs");
      var tab = OnTheUIThread(() => window.Tabs.Single());
      Render(window, "A-tab-of-a-Codex-session");
      return (window, tab);
   });

   [XF] public void the_tab_is_titled_with_the_sessions_name() => OnTheUIThread(() => _tab.Title).Must().Be(TheSession.Name!);

   [XF] public void the_tabs_agent_CLI_is_Codex() => OnTheUIThread(() => _tab.AgentCli).Must().ReferenceEqual(AgentCli.Codex);

   [XF] public void the_tab_shows_its_terminal() => OnTheUIThread(() => _tab.ShowsTheChat).Must().BeFalse();

   [XF] public void the_tab_offers_no_chat() => OnTheUIThread(() => _tab.OffersTheChat).Must().BeFalse();

   public class and_Magnus_presses_Ctrl_H : When_a_project_window_brings_back_a_tab_of_a_Codex_session
   {
      public and_Magnus_presses_Ctrl_H() => Press(_window, Key.H, PhysicalKey.H, RawInputModifiers.Control);

      [XF] public void the_tab_still_shows_its_terminal() => OnTheUIThread(() => _tab.ShowsTheChat).Must().BeFalse();
   }
}
