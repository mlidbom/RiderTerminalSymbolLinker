using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Chat;
using Cloyd.ClaudeCode;
using Cloyd.Hub;
using Cloyd.Saving;
using Cloyd.SessionHosts;
using Cloyd.Terminals;

namespace Cloyd.Sessions;

/// <summary>
/// A tab of a <see cref="Projects.ProjectWindow"/>: the terminal of one session host, showing its shell, and the Claude
/// Code in the shell as Claude Code draws its TUI; and the chat of the session that runs in it, or ran in it last. A
/// switch at its top shows the one or the other, as Ctrl+H does: the terminal at first, until the chat can do all Magnus
/// needs. Titled with the name of that session.
/// </summary>
sealed class SessionTab : TabItem
{
   const string TitleOfASessionWithNoName = "Claude";

   readonly AttachmentToASessionHost _attachment;
   readonly TextBlock _title;
   readonly TextBlock _notice = new() { IsVisible = false, Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 4) };
   readonly ToggleButton _showTheChat = SwitchTo("Chat", TipOfTheChatsSwitch);
   readonly ToggleButton _showTheTerminal = SwitchTo("Terminal", TipOfTheTerminalsSwitch);

   /// <summary>
   /// Attaches to <paramref name="host"/> at <paramref name="size"/>, which its terminal then resizes it from. Until the
   /// hub tells of the session that runs in it, its <see cref="LatestSession"/> is <paramref name="latestSession"/>, as a
   /// saved tab's, and it is titled with that session's name, or <paramref name="nameGiven"/>, as a new session was named.
   /// Its chat follows the session's conversation through <paramref name="hub"/>.
   /// </summary>
   public SessionTab(SessionHostOfTheHub host, ClaudeSession? latestSession, string? nameGiven, ConsoleSize size, TheHub hub)
   {
      Host = host;
      LatestSession = latestSession;
      _attachment = AttachmentToASessionHost.To(host.Id, size);
      _attachment.Ended += why => Dispatcher.UIThread.Post(() => EndedByItself?.Invoke(this, why));
      Terminal = new TerminalOfASessionHost(_attachment);
      Chat = new ChatOfASession(hub) { IsVisible = false };
      Chat.Show(latestSession?.Id);
      _title = new TextBlock { Text = latestSession?.Name ?? nameGiven ?? TitleOfASessionWithNoName, VerticalAlignment = VerticalAlignment.Center };
      Header = TheHeader();
      _showTheChat.Click += (_, _) => Show(chat: true);
      _showTheTerminal.Click += (_, _) => Show(chat: false);
      ShowTheChat(false);
      Content = new DockPanel { Children = { Docked(_notice, Dock.Top), Docked(TheSwitch(), Dock.Top), new Panel { Children = { Terminal, Chat } } } };
   }

   protected override Type StyleKeyOverride => typeof(TabItem);

   /// <summary>The tip of the switch's button that shows the chat: the key that switches too.</summary>
   public const string TipOfTheChatsSwitch = "Show the chat: Ctrl+H";

   /// <summary>The tip of the switch's button that shows the terminal: the key that switches too.</summary>
   public const string TipOfTheTerminalsSwitch = "Show the terminal: Ctrl+H";

   public SessionHostOfTheHub Host { get; }

   public TerminalOfASessionHost Terminal { get; }

   /// <summary>The chat of the session that runs in the tab's host, or ran in it last.</summary>
   public ChatOfASession Chat { get; }

   /// <summary>The tab's title: the name of the session that runs in its host, or ran in it last.</summary>
   public string Title => _title.Text!;

   /// <summary>What the tab tells above its terminal, as why the hub didn't start Claude Code in it: null while it tells nothing.</summary>
   public string? Notice => _notice.IsVisible ? _notice.Text : null;

   /// <summary>Raised when Magnus clicks the tab's ×, which is to end its session.</summary>
   public event Action<SessionTab>? ClosedByMagnus;

   /// <summary>Raised on the UI thread, with why, once the host has let the tab go: its shell exited, or another attach client took the session over.</summary>
   public event Action<SessionTab, string>? EndedByItself;

   /// <summary>The Claude Code that runs in the tab's host, as the hub last told: null while none runs, as before it starts, or after it exits to the shell.</summary>
   public RunningSession? SessionRunningInIt
   {
      get;
      set
      {
         field = value;
         if(value is null) return;
         LatestSession = value.Session;
         Chat.Show(value.Session.Id);
         _title.Text = value.Name ?? TitleOfASessionWithNoName;
      }
   }

   /// <summary>
   /// The session that runs in the tab's host, or ran in it last, as after Claude Code exits to the shell: what the tab
   /// is saved with, to be resumed once its host is gone. Null while none has, as before Claude Code starts.
   /// </summary>
   public ClaudeSession? LatestSession { get; private set; }

   /// <summary>Whether the Claude Code that runs in the tab's host runs as <paramref name="process"/>.</summary>
   public bool RunsTheClaudeCodeOf(ClaudeCodeProcessId process) => SessionRunningInIt?.Process == process;

   /// <summary>Whether <paramref name="session"/> runs in the tab's host, or ran in it last: its <see cref="LatestSession"/>.</summary>
   public bool RunsOrRanLast(ClaudeSessionId session) => LatestSession?.Id == session;

   /// <summary>The tab, as its window saves it.</summary>
   public SavedSessionTab AsSaved => new(Host.Id, Host.Directory, LatestSession);

   /// <summary>Shows <paramref name="notice"/> above the terminal.</summary>
   public void Tell(string notice)
   {
      _notice.Text = notice;
      _notice.IsVisible = true;
   }

   /// <summary>Shows the chat when the terminal is shown, else the terminal, as Ctrl+H does, and gives it the keyboard.</summary>
   public void SwitchBetweenTheTerminalAndTheChat() => Show(chat: !Chat.IsVisible);

   /// <summary>Gives the keyboard to what the tab shows: the terminal, which types into the session, or the chat.</summary>
   public void FocusWhatItShows()
   {
      if(Chat.IsVisible) Chat.TakeTheKeyboard();
      else Terminal.Focus();
   }

   /// <summary>Lets go of the tab's host, whose session runs on.</summary>
   public void LetGo() => _attachment.Dispose();

   /// <summary>
   /// Shows the chat when <paramref name="chat"/>, else the terminal, as Magnus asks, and gives it the keyboard once it is
   /// laid out: a chat shown for the first time isn't, and takes no focus until it is.
   /// </summary>
   void Show(bool chat)
   {
      ShowTheChat(chat);
      Dispatcher.UIThread.Post(FocusWhatItShows, DispatcherPriority.Loaded);
   }

   /// <summary>Shows the chat when <paramref name="chat"/>, else the terminal, as the switch shows.</summary>
   void ShowTheChat(bool chat)
   {
      Chat.IsVisible = chat;
      _showTheChat.IsChecked = chat;
      Terminal.IsVisible = !chat;
      _showTheTerminal.IsChecked = !chat;
   }

   Control TheSwitch() => new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 2, Margin = new Thickness(4, 2), Children = { _showTheChat, _showTheTerminal } };

   static ToggleButton SwitchTo(string view, string tip)
   {
      var button = new ToggleButton { Content = view, Padding = new Thickness(10, 2), FontSize = 12 };
      ToolTip.SetTip(button, tip);
      return button;
   }

   Control TheHeader()
   {
      var close = new Button { Content = "×", Padding = new Thickness(6, 0), Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center };
      ToolTip.SetTip(close, "End the session");
      close.Click += (_, _) => ClosedByMagnus?.Invoke(this);
      return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { _title, close } };
   }

   static Control Docked(Control control, Dock dock)
   {
      DockPanel.SetDock(control, dock);
      return control;
   }
}
