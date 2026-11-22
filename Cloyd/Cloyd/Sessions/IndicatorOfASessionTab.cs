using Avalonia.Controls;
using Avalonia.Layout;
using Cloyd.Chat.Status;
using Cloyd.Codex;
using Cloyd.Hub.RunningSessions;

namespace Cloyd.Sessions;

/// <summary>
/// The mark before a <see cref="SessionTab"/>'s title. It identifies the tab's <see cref="AgentCli"/> even
/// when idle and reserves the same space when a turn starts or Claude Code leaves work running in the background.
/// </summary>
sealed class IndicatorOfASessionTab : Decorator
{
   readonly SpinnerOfClaudeCode _claude = new();
   readonly MarkerOfBackgroundWork _backgroundWork = new();
   readonly CodexProgressDot _codex = new();

   public IndicatorOfASessionTab()
   {
      Width = SpinnerOfClaudeCode.HowWideItIs;
      Height = SpinnerOfClaudeCode.HowHighItIs;
      VerticalAlignment = VerticalAlignment.Center;
   }

   public bool ShowsThatItsSessionWorks => _claude.IsTurning || _codex.IsPulsing;
   public string? BackgroundWorkMarked => _backgroundWork.Tip;

   /// <summary>Shows the <paramref name="agentCli"/> the tab belongs to, including after its session exits.
   /// Only a busy <paramref name="session"/> animates its mark.</summary>
   public void Show(AgentCli agentCli, RunningSession? session)
   {
      _claude.IsTurning = session is RunningClaudeCode { IsBusy: true };
      _codex.IsPulsing = session is RunningCodex { IsBusy: true };
      _backgroundWork.Show(session as RunningClaudeCode);
      Child = agentCli == AgentCli.Codex ? _codex : ClaudeCodeMark();

      Control ClaudeCodeMark() => _backgroundWork.IsVisible ? _backgroundWork : _claude;
   }
}
