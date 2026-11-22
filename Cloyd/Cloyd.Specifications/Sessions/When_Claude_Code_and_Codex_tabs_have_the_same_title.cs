using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Cloyd.Chat.Status;
using Cloyd.Chat.Views;
using Cloyd.Codex;
using Cloyd.Projects;
using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Sessions;

/// <summary>Two sessions named Review, with Codex selected. Their tab indicators distinguish them even before either starts a turn.</summary>
public class When_Claude_Code_and_Codex_tabs_have_the_same_title : SpecificationOpeningWindows
{
   readonly ProjectWindow _window;
   readonly SessionTab _claudeTab, _codexTab;
   readonly SpinnerOfClaudeCode _star;
   readonly CodexProgressDot _dot;
   readonly IReadOnlyList<Rect> _idleHeaders;
   readonly IReadOnlyList<(Color Star, Color Dot)> _idleColours;

   public When_Claude_Code_and_Codex_tabs_have_the_same_title()
   {
      (_window, _claudeTab, _codexTab, _star, _dot) = Once(() =>
      {
         var window = Open(Sandbox.Project("tab-indicators").Window);
         OnTheUIThread(() => { window.Width = 760; window.Height = 420; });
         foreach(var agent in new[] { AgentCli.ClaudeCode, AgentCli.Codex })
            DoneOnTheUIThread(() => window.StartANewSession(new NewSession(agent, "Review")));
         var tabs = OnTheUIThread(() => window.Tabs);
         WaitUntil(() => tabs.All(it => it.SessionRunningInIt?.Session.Name == "Review"), "the hub named both sessions");
         return OnTheUIThread(() => (window, tabs[0], tabs[1], Mark<SpinnerOfClaudeCode>(tabs[0]), Mark<CodexProgressDot>(tabs[1])));

         static TMark Mark<TMark>(SessionTab tab) where TMark : Control => ((Control)tab.Header!).FindDescendantOfType<TMark>()!;
      });
      (_idleColours, _idleHeaders) = Once(() => (RenderInBothThemes("idle"), HeaderBounds()));
   }

   [XF] public void the_idle_Claude_tab_shows_a_stationary_star() => OnTheUIThread(() => (_star.IsEffectivelyVisible, _star.IsTurning, _star.Text)).Must().Be((true, false, "✶"));

   [XF] public void the_idle_Codex_tab_shows_a_stationary_dot() => OnTheUIThread(() => (_dot.IsEffectivelyVisible, _dot.IsPulsing)).Must().Be((true, false));

   [XF] public void both_idle_marks_are_grey_in_each_theme() =>
      _idleColours.Must().SequenceEqual(new[] { ThemeVariant.Light, ThemeVariant.Dark }.Select(it => (ColoursOfTheChat.IdleClaudeStar.In(it), ColoursOfTheChat.IdleCodexDot.In(it))));

   [XF] public void the_idle_Codex_dot_uses_a_lighter_grey_than_the_Claude_star() =>
      _idleColours.Must().Satisfy(it => it.All(marks => marks.Dot.R > marks.Star.R));

   static bool HasTheAgentColours((Color Star, Color Dot) marks) =>
      marks.Star.R > marks.Star.G && marks.Star.G > marks.Star.B && marks.Dot.B > marks.Dot.G && marks.Dot.G > marks.Dot.R;

   IReadOnlyList<Rect> HeaderBounds() => OnTheUIThread(() => _window.Tabs.Select(it => it.Bounds).ToList());

   IReadOnlyList<(Color Star, Color Dot)> RenderInBothThemes(string state)
   {
      List<(Color Star, Color Dot)> colours = [];
      foreach(var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
      {
         OnTheUIThread(() => _window.RequestedThemeVariant = theme);
         WaitUntil(() => _star.ActualThemeVariant == theme && _dot.ActualThemeVariant == theme, "both tabs took the requested theme");
         Render(_window, $"session-tab-indicators-{state}-{theme}");
         colours.Add(OnTheUIThread(() => (((ISolidColorBrush)_star.Foreground!).Color, ((ISolidColorBrush)_dot.Background!).Color)));
      }
      return colours;
   }

   public class and_both_sessions_start_a_turn : When_Claude_Code_and_Codex_tabs_have_the_same_title
   {
      readonly CodexProgressDot _chatDot;
      readonly IReadOnlyList<(Color Star, Color Dot)> _workingColours;

      public and_both_sessions_start_a_turn() => (_chatDot, _workingColours) = Once(() =>
      {
         foreach(var tab in new[] { _claudeTab, _codexTab })
            DoneOnTheUIThread(() => tab.Terminal.SendInputAsync("Be busy for 60 seconds\r"));
         WaitUntil(() => _star.IsTurning && _dot.IsPulsing && _codexTab.CodexChat!.Progress.IsVisible, "both tab indicators and the Codex chat showed the turns");
         var chatDot = OnTheUIThread(() => _codexTab.CodexChat!.Progress.FindDescendantOfType<CodexProgressDot>()!);
         WaitUntil(() => ScaleOf(chatDot) > 0.85, "the dot in the chat grew");
         WaitUntil(() => ScaleOf(_dot) > 0.85, "the dot in the tab grew");
         var colours = RenderInBothThemes("working");
         WaitUntil(() => ScaleOf(_dot) < 0.75, "the tab's dot shrank again");
         return (chatDot, colours);

         static double ScaleOf(CodexProgressDot dot) => dot.RenderTransform!.Value.M11;
      });

      [XF] public void Claude_Code_uses_its_spinner() => OnTheUIThread(() => _star is { IsEffectivelyVisible: true, IsTurning: true }).Must().BeTrue();

      [XF] public void the_working_star_is_warm_and_the_working_dot_blue_in_both_themes() => _workingColours.Must().Satisfy(it => it.All(HasTheAgentColours));

      [XF] public void the_working_Codex_dot_uses_the_same_blue_in_both_themes() =>
         _workingColours[0].Dot.Must().Be(_workingColours[1].Dot);

      [XF] public void the_dot_in_the_header_and_chat_peaks_at_the_width_of_Claudes_spinner_glyphs() =>
         OnTheUIThread(() => (_dot.Bounds.Width, _dot.Bounds.Height, _chatDot.Bounds.Width, _chatDot.Bounds.Height)).Must().Be((11d, 11d, 11d, 11d));

      [XF] public void the_Codex_chat_shows_the_pulsing_dot_beside_its_activity() =>
         OnTheUIThread(() => _chatDot is { IsEffectivelyVisible: true, IsPulsing: true }).Must().BeTrue();

      [XF] public void the_tab_headers_keep_their_idle_positions_and_sizes() => HeaderBounds().Must().SequenceEqual(_idleHeaders);
   }
}
