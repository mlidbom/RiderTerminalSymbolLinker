using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Chat;
using Cloyd.Hub;
using Cloyd.Hub.RunningSessions;
using Cloyd.Saving;
using Cloyd.Terminals;

namespace Cloyd.Sessions;

/// <summary>The session in one host, shared by its project tab and Projects Dashboard. Its terminal keeps
/// reading across a move, and its chat state belongs to the host whichever window edits it.</summary>
sealed class HostedSession
{
   readonly SessionsOfCloyd _sessions;
   readonly NewSession? _startedAs;
   readonly ConsoleSize _size;
   readonly List<SessionView> _views = [];
   SessionView? _showingTerminal;
   TerminalInCloyd? _terminal;

   public HostedSession(SessionsOfCloyd sessions, SessionHostOfTheHub host, Session? latest, NewSession? startedAs, ConsoleSize size, SavedSessionTab? saved)
   {
      _sessions = sessions;
      Host = host;
      LatestSession = saved?.Session ?? latest;
      _startedAs = startedAs;
      _size = size;
      ChatState = new SessionChatState(sessions.Hub, sessions.Images);
      if(saved is not null) ChatState.Restore(saved);
   }

   public SessionHostOfTheHub Host { get; }
   public Session? LatestSession { get; private set; }
   public RunningSession? Running { get; private set; }
   /// <summary>The agent identified by a previous or requested session. A shell can have neither.</summary>
   public AgentCli? KnownAgentCli => LatestSession?.AgentCli ?? _startedAs?.AgentCli;
   public AgentCli AgentCli => KnownAgentCli ?? AgentCli.ClaudeCode;
   public string Title => LatestSession?.Name ?? _startedAs?.Title ?? AgentCli.TitleOfASessionWithNoName;
   public SessionChatState ChatState { get; }
   public bool HasAttachment => _terminal?.HasAttachment == true;
   public bool HasProjectTab => _views.Any(it => it.InProjectWindow);
   public bool HasViews => _views.Count > 0;
   TerminalInCloyd SharedTerminal => _terminal ??= new TerminalInCloyd(Host, _size, _sessions.Hub, _sessions.Images, () => AgentCli, HostEnded);
   public TerminalOfASessionHost Terminal => SharedTerminal.Control;
   public bool OffersCodexChat => _sessions.OffersCodexChat;

   public event Action? Changed;
   public event Action<string>? Ended;

   public void Update(RunningSession? running)
   {
      Running = running;
      ChatState.ClaudeCode.Turn.Show(running as RunningClaudeCode);
      if(running is not null) LatestSession = running.Session;
      Changed?.Invoke();
   }

   public void AddView(SessionView view, bool showTerminal)
   {
      _views.Add(view);
      if(showTerminal && _showingTerminal is null) ShowTerminalIn(view);
   }

   public bool ShowsTerminalIn(SessionView view) => _showingTerminal == view;

   public void ShowTerminalIn(SessionView view, bool takeOver = false)
   {
      if(takeOver) SharedTerminal.AllowTakeover();
      if(_showingTerminal == view) return;
      _showingTerminal?.ReleaseTerminal();
      _showingTerminal = view;
      view.ReceiveTerminal(Terminal);
   }

   public void RemoveView(SessionView view)
   {
      _views.Remove(view);
      if(_showingTerminal != view) return;
      view.ReleaseTerminal();
      _showingTerminal = null;
      if(_views.FirstOrDefault() is {} remaining) ShowTerminalIn(remaining);
   }

   public SavedSessionTab AsSaved => new(Host.Id, Host.Directory, LatestSession, ChatState.ClaudeCodeDraft, ChatState.CodexDrafts);

   /// <summary>Releases the host when its last window closes or it passes to an external attach client.</summary>
   public void LetGo()
   {
      _terminal?.Dispose();
      _terminal = null;
   }

   public void HostEnded(string why)
   {
      Ended?.Invoke(why);
      LetGo();
   }
}
