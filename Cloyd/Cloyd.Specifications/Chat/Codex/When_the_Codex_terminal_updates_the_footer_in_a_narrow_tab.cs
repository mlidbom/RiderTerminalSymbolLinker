using System.Text.Json;
using Avalonia.Input;
using Cloyd.Codex.Chat;
using Cloyd.Codex.Chat.Status;
using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Codex;

/// <summary>A real session tab in the sandbox receives the configured fields and then the native token
/// and rate-limit notifications. Even the last field fits, by wrapping in the chat.</summary>
public class When_the_Codex_terminal_updates_the_footer_in_a_narrow_tab : SpecificationOpeningWindows
{
   readonly SessionTab _tab;
   ChatOfACodexSession Chat => _tab.CodexChat!;

   public When_the_Codex_terminal_updates_the_footer_in_a_narrow_tab() => _tab = Once(() =>
   {
      string[] items = ["thread-id", "model-with-reasoning", "current-dir", "thread-name", "context-used", "weekly-limit", "used-tokens", "total-input-tokens", "total-output-tokens"];
      var home = Sandbox.HubOfTheSandbox.CodexFolder;
      File.WriteAllText(Path.Combine(home, "config.toml"), $"service_tier = \"priority\"\n[tui]\nstatus_line = {JsonSerializer.Serialize(items)}\nstatus_line_use_colors = true\n");
      File.WriteAllText(Path.Combine(home, "models_cache.json"), """{"models":[{"slug":"stand-in","display_name":"Sandbox Model","service_tiers":[{"id":"priority","name":"Fast"}]}]}""");
      var window = Open(Sandbox.Project("footer-check").Window);
      OnTheUIThread(() => { window.Width = 640; window.Height = 500; });
      DoneOnTheUIThread(() => window.StartANewSession(new NewSession(AgentCli.Codex, "Footer check")));
      var tab = OnTheUIThread(() => window.Tabs.Single());
      WaitUntil(() => tab.CodexChat?.InputBox.CanSend == true && tab.CodexChat.StatusLine.TextShown.Contains("Sandbox Model xhigh fast"),
                "the footer identified the running model and its tier", () => tab.CodexChat?.StatusLine.TextShown ?? tab.Title);
      var chat = OnTheUIThread(() => tab.CodexChat!);
      OnTheUIThread(() => chat.StatusLine.TextShown.Contains(" out")).Must().BeFalse();
      WaitUntilTheInputBoxHasTheKeyboard(window, tab);
      Type(window, "Check the status line");
      Press(window, Key.Enter, PhysicalKey.Enter, RawInputModifiers.Control);
      WaitUntil(() => chat.StatusLine.TextShown.EndsWith("1K out", StringComparison.Ordinal) && chat.Shown.LastOrDefault() is EndOfACodexTurn,
                "the rate limit and token counters arrived from the hub", () => chat.StatusLine.TextShown);
      Render(window, "Codex-status-after-the-turn-in-a-narrow-tab");
      return tab;
   });

   [XF] public void Chat_starts_with_the_model_and_the_session_name_stays_in_the_tab() =>
      OnTheUIThread(() => _tab.Title == "Footer check" && Chat.StatusLine.TextShown.StartsWith("Sandbox Model xhigh fast · ", StringComparison.Ordinal)
                                                      && !Chat.StatusLine.TextShown.Split(" · ").Contains(_tab.Title)).Must().BeTrue();

   [XF] public void the_terminal_still_exposes_the_session_id_for_Peers() =>
      OnTheUIThread(() => _tab.Terminal.TextShown.Contains(_tab.SessionRunningInIt!.Session.Id.Value, StringComparison.Ordinal)).Must().BeTrue();

   [XF] public void the_native_usage_figures_arrive_with_their_distinct_meanings() =>
      OnTheUIThread(() => Chat.StatusLine.TextShown).Must().Satisfy(it => it.EndsWith("Context 10% used · week 1% · 5K used · 20K in · 1K out", StringComparison.Ordinal));

   [XF] public void the_status_line_keeps_the_reported_permission_settings() =>
      OnTheUIThread(() => Chat.StatusLine.PermissionsShown).Must().Be("Workspace · Ask for approval");
}
