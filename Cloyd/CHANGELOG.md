# Changelog

All notable changes to Cloyd are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0]

### Added
- **A window for each project, beside its IDE, whose tabs are the project's Claude Code sessions.** As a project opens
  in an IDE, Claude Sessions 0.2.0 has Cloyd show its window. Each tab shows a session in its terminal: the terminal of
  the session's host, which runs outside both Cloyd and the IDE, so the session runs on as either exits. The **+**
  starts a new session, named or not, and each tab is titled with its session's name. Closing a tab ends its session;
  closing the window leaves the sessions running. A tab closes when its host lets it go: when another client takes the
  session over, or the session's shell exits.
- **One Cloyd application shows every project's window.** `Cloyd.exe --project <folder>` hands the project to it,
  starting it when it doesn't run, and brings the project's window forward when it shows already. It runs outside the
  IDEs, so it stays as an IDE exits, and it ends as its last window closes.
- **The windows keep their tabs.** Each window saves its tabs every 2 seconds and as it closes, and brings them back as
  it opens, in their order, with the selected tab selected. A session whose host still runs is shown again as it is; one
  whose host is gone, as after a reboot, is resumed in a new host with `claude --resume`, unless its transcript is gone.
- **Each project's window goes back to its virtual desktop.** A window saves the desktop it is on with its tabs, and
  goes back to it as it opens. One with no desktop saved, as on its first opening, goes to the desktop of the project's
  IDE window once an IDE shows the project. It follows neither afterwards: where you move a window, it stays, also
  through restarts. An IDE opening the project doesn't bring forward a Cloyd window on another desktop, since Windows
  would switch to that desktop with it; a notification's click still does.
- **A notification's click shows the session's tab again,** as the IDE's tool window did until Claude Sessions 0.2.0:
  it selects the tab the session runs in, or ran in last, and brings its window forward.
- **A Ctrl+click on an http or https URL in a terminal opens it in the browser.**
- **Each tab can show its session as a chat.** A switch at the top of each tab, **Chat** and **Terminal**, shows the
  conversation as the Claude desktop app's Code tab does: your prompts in bubbles, Claude's replies as rendered
  markdown, tool calls folded into a line that counts them and opens to each, a command's with its output, and a line
  where the conversation was compacted. It shows what Claude Code's transcript holds, and can't send yet, so each tab
  shows its terminal at first.
- **Ctrl+H switches a tab between its terminal and its chat,** as the switch at its top does, and gives the keyboard to
  the one it shows. Claude Code binds Ctrl+H to no command of its own: in the terminal it was a Backspace.
- **Cloyd follows Windows' theme, light or dark,** as the desktop app does. The terminal stays dark.
- **The session history picker,** as the IDE's was until Claude Sessions 0.2.0. The **History** button beside the
  **+**, or Ctrl+Alt+Shift+Y, lays it over the window: the sessions, most recently used first, filtered by every word
  typed, of this worktree, of all the project's worktrees or of all projects. Sessions with no conversation, and those
  `claude -p` ran, aren't listed. Choosing a session brings forward the tab it runs in, in any of Cloyd's windows;
  resumes it in the tab it exited in; shows in a new tab a session host that no tab shows; or else resumes it in a new
  tab in the project's folder. A session that runs outside any session host can't be chosen.

### Fixed
- **Shift+Enter types a new line in Claude Code's input box,** where it sent the message: in a Windows console Claude
  Code ignores the Shift on Enter, so Cloyd types Ctrl+J, Claude Code's key for a new line.
- **A long conversation no longer hangs Cloyd.** Opened on a long session, the chat flickered and Cloyd stopped
  answering. The chat now draws the conversation's last 100 messages and tool calls, and 100 more each time you scroll
  up near its top, keeping in place what you were looking at.
- **A tab that closes no longer keeps a processor core busy.** Each terminal whose session ended in it, as its tab
  closed or its host let it go, kept a core busy for 30 seconds.
