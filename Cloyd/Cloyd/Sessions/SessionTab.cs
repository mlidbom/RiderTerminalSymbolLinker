using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Cloyd.Chat;
using Cloyd.Codex.Chat;
using Cloyd.ClaudeCode;
using Cloyd.Hub;
using Cloyd.Hub.RunningSessions;
using Cloyd.Saving;
using Cloyd.Terminals;

namespace Cloyd.Sessions;

/// <summary>
/// A tab of a <see cref="Projects.ProjectWindow"/>: the terminal of one session host, showing its shell, and the agent
/// CLI in the shell as it draws its TUI; and the chat of the session that runs in it, or ran
/// in it last. A switch at its top shows the one or the other, as Ctrl+H does: the chat at first, the terminal staying
/// the backup, and where Claude Code's own dialogs show, as its permission prompts. Titled with the name of that session.
/// </summary>
/// <remarks>
/// Its look follows its <see cref="AgentCli"/>, as a host's shell may run one agent CLI and then another: a Codex
/// session has a chat when the hub advertises support for it.
/// </remarks>
sealed class SessionTab : TabItem
{
   readonly SessionTabHeader _header = new();
   public HostedSession Session { get; }
   public SessionView View { get; }

   public SessionTab(HostedSession session, bool takeOver = false)
   {
      Session = session;
      View = new SessionView(session, inProjectWindow: true, showTerminal: true);
      if(takeOver) session.ShowTerminalIn(View, takeOver: true);
      Content = View;
      Header = _header;
      _header.EndRequested += (_, _) => ClosedByMagnus?.Invoke(this);
      session.Changed += ShowHeader;
      session.Ended += Ended;
      ShowHeader();
   }

   void ShowHeader() => _header.Heading = new SessionTabHeading(Session.Title, Session.KnownAgentCli, Session.Running);

   public void Release(SessionsOfCloyd sessions)
   {
      Session.Changed -= ShowHeader;
      Session.Ended -= Ended;
      _header.Heading = null;
      sessions.Release(View);
   }

   protected override Type StyleKeyOverride => typeof(TabItem);

   /// <summary>The tip of the switch's button that shows the chat: the key that switches too.</summary>
   public const string TipOfTheChatsSwitch = SessionView.TipOfTheChatsSwitch;
   public const string TipOfTheTerminalsSwitch = SessionView.TipOfTheTerminalsSwitch;
   public SessionHostOfTheHub Host => Session.Host;
   public TerminalOfASessionHost Terminal => Session.Terminal;
   public ChatOfAClaudeCodeSession ClaudeCodeChat => View.ClaudeCodeChat;
   public ChatOfACodexSession? CodexChat => View.CodexChat;
   public SessionChat? Chat => View.Chat;
   public AgentCli AgentCli => Session.AgentCli;
   public bool OffersTheChat => View.OffersTheChat;
   public string Title => Session.Title;
   public bool ShowsThatItsSessionWorks => _header.Indicator.ShowsThatItsSessionWorks;
   public string? BackgroundWorkMarked => _header.Indicator.BackgroundWorkMarked;
   public string? Notice => View.Notice;
   public event Action<SessionTab>? ClosedByMagnus;
   public event Action<SessionTab>? EndedByItself;
   void Ended(string why) => EndedByItself?.Invoke(this);
   public RunningSession? SessionRunningInIt => Session.Running;
   public Session? LatestSession => Session.LatestSession;
   public bool RunsTheClaudeCodeOf(ClaudeCodeProcessId process) => (SessionRunningInIt as RunningClaudeCode)?.Process == process;
   public bool RunsOrRanLast(SessionId session) => LatestSession?.Id == session;
   public SavedSessionTab AsSaved => Session.AsSaved;
   public bool ShowsTheChat => View.ShowsTheChat;
   public void SwitchBetweenTheTerminalAndTheChat() => View.SwitchBetweenTheTerminalAndTheChat();
   public void FocusWhatItShows() => View.FocusWhatItShows();

}
