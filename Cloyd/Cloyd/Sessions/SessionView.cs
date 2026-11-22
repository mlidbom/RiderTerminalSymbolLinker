using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Cloyd.Chat;
using Cloyd.Codex;
using Cloyd.Codex.Chat;
using Cloyd.ClaudeCode;
using Cloyd.Hub;
using Cloyd.Hub.RunningSessions;
using Cloyd.Terminals;
using Cloyd.Theming;

namespace Cloyd.Sessions;

/// <summary>The chat and terminal shown for a <see cref="HostedSession"/> in either window.
/// Each view has its own focus, scroll position and Chat/Terminal preference.</summary>
sealed class SessionView : UserControl
{
   readonly TextBlock _notice = new() { IsVisible = false, Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 4) };
   readonly ToggleButton _showTheChat = SwitchTo("Chat", TipOfTheChatsSwitch);
   readonly ToggleButton _showTheTerminal = SwitchTo("Terminal", TipOfTheTerminalsSwitch);
   readonly StackPanel _switch;
   readonly ContentControl _chatView = new();
   readonly Border _terminalSite = new();
   readonly Button _bringTerminal = new() { Content = "Show the terminal here", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
   bool _prefersChat = true;
   bool _offeredCodexChat;
   public HostedSession Session { get; }
   public bool InProjectWindow { get; }

   public SessionView(HostedSession session, bool inProjectWindow, bool showTerminal)
   {
      Session = session;
      InProjectWindow = inProjectWindow;
      _offeredCodexChat = session.OffersCodexChat;
      ClaudeCodeChat = session.ChatState.CreateClaudeCodeChat();
      ClaudeCodeChat.IsVisible = false;
      ClaudeCodeChat.InputBox.SentASlashCommand += () => Show(chat: false);
      ClaudeCodeChat.WaitingForMagnusInTheTerminal.AsksForTheTerminal += () => Show(chat: false);
      _showTheChat.Click += (_, _) => Show(chat: true);
      _showTheTerminal.Click += (_, _) => Show(chat: false);
      _switch = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 2, Margin = new Thickness(4, 2), Children = { _showTheChat, _showTheTerminal } };
      _bringTerminal.Click += (_, _) => Show(chat: false);
      Content = LookOfTheTabs.Ground.AsTheBackgroundOf(new Border { Child = new DockPanel { Children = { Docked(_notice, Dock.Top), Docked(_switch, Dock.Top), new Panel { Children = { _terminalSite, _bringTerminal, _chatView } } } } });
      session.Changed += ShowSession;
      session.AddView(this, showTerminal);
      ShowSession();
   }

   protected override Type StyleKeyOverride => typeof(UserControl);

   /// <summary>The tip of the switch's button that shows the chat: the key that switches too.</summary>
   public const string TipOfTheChatsSwitch = "Show the chat: Ctrl+H";

   /// <summary>The tip of the switch's button that shows the terminal: the key that switches too.</summary>
   public const string TipOfTheTerminalsSwitch = "Show the terminal: Ctrl+H";

   public SessionHostOfTheHub Host => Session.Host;

   public TerminalOfASessionHost Terminal => Session.Terminal;

   /// <summary>The chat of the session that runs in the tab's host, or ran in it last: shown only while that is Claude Code's.</summary>
   public ChatOfAClaudeCodeSession ClaudeCodeChat { get; }

   /// <summary>The Codex view once the hub has offered Chat in this Codex tab.</summary>
   public ChatOfACodexSession? CodexChat { get; private set; }

   /// <summary>The conversation belonging to the agent CLI that this tab shows.</summary>
   public SessionChat? Chat => AgentCli == AgentCli.Codex ? CodexChat : ClaudeCodeChat;

   public AgentCli AgentCli => Session.AgentCli;

   /// <summary>Whether the tab shows its switch between the terminal and the chat: while its <see cref="AgentCli"/>'s sessions have a chat.</summary>
   public bool OffersTheChat => _switch.IsVisible;

   /// <summary>What the tab tells above its terminal, as why the hub didn't start Claude Code in it: null while it tells nothing.</summary>
   public string? Notice => _notice.IsVisible ? _notice.Text : null;

   void ShowSession()
   {
      if(_offeredCodexChat != Session.OffersCodexChat && AgentCli == AgentCli.Codex && !ShowsTheChat) _prefersChat = false;
      _offeredCodexChat = Session.OffersCodexChat;
      ClaudeCodeChat.ClaudeCode = Session.Running as RunningClaudeCode;
      ClaudeCodeChat.Show((Session.LatestSession as ClaudeCodeSession)?.Id);
      FollowItsAgentCli();
      CodexChat?.Show((Session.LatestSession as CodexSession)?.Id, Session.Running as RunningCodex);
   }

   /// <summary>Shows <paramref name="notice"/> above the terminal, as why the hub didn't start Claude Code in it, or why a paste failed.</summary>
   public void Tell(string notice)
   {
      _notice.Text = notice;
      _notice.IsVisible = true;
   }

   /// <summary>Whether the tab shows its chat: else its terminal.</summary>
   public bool ShowsTheChat => _chatView.IsVisible;

   /// <summary>
   /// Shows the chat when the terminal is shown, else the terminal, as Ctrl+H does, and gives it the keyboard: while it
   /// <see cref="OffersTheChat"/>, else it keeps to its terminal.
   /// </summary>
   public void SwitchBetweenTheTerminalAndTheChat()
   {
      if(OffersTheChat) Show(chat: !ShowsTheChat);
      else Show(chat: false);
   }

   /// <summary>Gives the keyboard to what the tab shows: the terminal, which types into the session, or the chat.</summary>
   public void FocusWhatItShows()
   {
      if(ShowsTheChat) Chat!.TakeTheKeyboard();
      else if(Session.ShowsTerminalIn(this)) Terminal.Focus();
      else _bringTerminal.Focus();
   }

   /// <summary>Lets go of the tab's host, whose session runs on.</summary>
   public void LetGo()
   {
      Session.Changed -= ShowSession;
      Session.RemoveView(this);
      ClaudeCodeChat.IsVisible = false;
      ClaudeCodeChat.InputBox.Release();
      if(CodexChat is {} codex)
      {
         codex.IsVisible = false;
         codex.InputBox.Release();
      }
   }

   /// <summary>
   /// Shows the chat when <paramref name="chat"/>, else the terminal, as Magnus asks, and gives it the keyboard once it is
   /// laid out: a chat shown for the first time isn't, and takes no focus until it is.
   /// </summary>
   void Show(bool chat)
   {
      if(!chat) Session.ShowTerminalIn(this, takeOver: true);
      _prefersChat = chat;
      ShowTheChat(chat);
      Dispatcher.UIThread.Post(FocusWhatItShows, DispatcherPriority.Loaded);
   }

   /// <summary>
   /// Shows the chat when <paramref name="chat"/>, else the terminal, as the switch shows. The terminal stays laid out under
   /// the chat, unseen and out of the mouse's reach: it attaches to its host only once laid out, and reads what the session
   /// draws, at the size of the tab, only while it is.
   /// </summary>
   void ShowTheChat(bool chat)
   {
      var active = Chat;
      _chatView.Content = active;
      _chatView.IsVisible = chat;
      ClaudeCodeChat.IsVisible = chat && active == ClaudeCodeChat;
      if(CodexChat is {} codex) codex.IsVisible = chat && active == codex;
      _showTheChat.IsChecked = chat;
      _terminalSite.Opacity = chat ? 0 : 1;
      _terminalSite.IsHitTestVisible = !chat;
      _showTheTerminal.IsChecked = !chat && _terminalSite.Child is not null;
      _bringTerminal.IsVisible = !chat && _terminalSite.Child is null;
   }

   /// <summary>
   /// Gives the tab the look of its <see cref="AgentCli"/>: the switch to the chat while that has one, else no switch, and
   /// the terminal shown.
   /// </summary>
   void FollowItsAgentCli()
   {
      if(AgentCli == AgentCli.Codex && _offeredCodexChat && CodexChat is null)
      {
         CodexChat = Session.ChatState.CreateCodexChat();
         CodexChat.IsVisible = false;
         CodexChat.WaitingForMagnusInTheTerminal.AsksForTheTerminal += () => Show(chat: false);
      }
      _switch.IsVisible = AgentCli == AgentCli.ClaudeCode || _offeredCodexChat;
      ShowTheChat(_prefersChat && OffersTheChat);
   }

   static ToggleButton SwitchTo(string view, string tip)
   {
      var button = new ToggleButton { Content = view, Padding = new Thickness(10, 2), FontSize = 12 };
      ToolTip.SetTip(button, tip);
      return button;
   }

   public void ReceiveTerminal(TerminalOfASessionHost terminal)
   {
      _terminalSite.Child = terminal;
      terminal.PasteFailed += PasteFailed;
      terminal.AttachmentFailed += AttachmentFailed;
      ShowTheChat(_prefersChat && OffersTheChat);
   }

   public void ReleaseTerminal()
   {
      if(_terminalSite.Child is TerminalOfASessionHost terminal)
      {
         terminal.PasteFailed -= PasteFailed;
         terminal.AttachmentFailed -= AttachmentFailed;
      }
      _terminalSite.Child = null;
      _prefersChat = true;
      ShowTheChat(OffersTheChat);
   }

   void PasteFailed(Exception failure) => Tell($"Cloyd couldn't paste: {failure.Message}");
   void AttachmentFailed(Exception failure) => Tell($"Cloyd couldn't attach to the session host: {failure.Message}");

   static Control Docked(Control control, Dock dock)
   {
      DockPanel.SetDock(control, dock);
      return control;
   }
}
