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
  where the conversation was compacted. It shows what Claude Code's transcript holds, and what Claude writes as it comes.
  Each tab shows its chat at first; its terminal, where Claude Code's own dialogs show, is a click or Ctrl+H away.
- **The chat has an input box, which sends to Claude Code,** as Claude Code's own box would: under the conversation, as
  the desktop app's composer, growing with what you write up to two fifths of the chat, then scrolling.
  - **Ctrl+Enter sends, Enter starts a new line.** The box is free for the next message at once.
  - **Nothing interrupts Claude by accident.** Escape does nothing to Claude, and a message sent while Claude works
    waits: Claude Code queues it, and takes it when it is done, as it does those sent in its own box. To interrupt
    Claude, the **Stop** button shows beside the send button while Claude works.
  - **A message sent stays in sight until Claude Code gives it to Claude,** over the box, dimmed, as Claude Code's own
    UI shows a queued message: `Sending…` while it is typed into Claude Code, `Queued` once Claude Code has queued it.
    It goes once the conversation has it.
  - **Ctrl+V keeps what you paste apart from what you type,** as Claude Code's own box does: a long paste reaches
    Claude as pasted. An image on the clipboard with no text is pasted as `[Image #1]`, with a thumbnail over the box
    that a click shows whole; Claude gets the image.
  - **A message Claude Code didn't get comes back into the box,** ahead of what you wrote since, with why over it in
    red: such as when Claude Code's own box in the terminal holds text, which the message would have mangled.
  - **A slash command,** such as `/compact`, switches the tab to its terminal, where Claude Code shows what it does.
  - **What the box holds comes back with the window,** after Cloyd restarts, typed and pasted parts as they came in.
- **A status line under the chat's input box,** as the desktop app's footer: at its left, whether Claude waits for you
  in the terminal (and for what, such as a permission prompt), or is idle; at its right, Claude Code's own status line,
  such as "Opus 5.5 · xhigh · Auto · 23% context", which shows once Claude Code has given it to the hub, within a minute
  of its start.
- **A working line over the chat's input box while Claude works,** as Claude Code's own UI has its spinner line, such
  as `Thinking… (1m 12s · ↓ 1.2k tokens · 3 commands)`: what Claude does, waiting for the model, thinking, writing,
  writing a tool call such as `Writing the Write call…`, or the tool call under way; how long the turn has taken; its
  output tokens; and how many commands it has run. A command under way shows as Claude described it, with the command
  under it, as `└ $ ls /c/Dev`; a file's tool call with the file's path. Where Claude Code doesn't tell the hub of its
  turn, the line says `Working…`, or the tool call under way, and counts the time from when Claude started working.
- **A box in Claude's orange between the conversation and the input box while Claude waits for you in the terminal,**
  such as at a permission prompt, which only the terminal shows and answers: it says what Claude waits for, and its
  **Show the terminal** button switches the tab to the terminal, as Ctrl+H does. The tab doesn't switch by itself, so
  that keys meant for the chat don't answer the prompt.
- **What Claude writes shows in the chat as it comes,** at the conversation's end, before Claude Code writes it to the
  transcript. Text a turn cut short, as when you stop Claude, goes with the turn.
- **Ctrl+H switches a tab between its terminal and its chat,** as the switch at its top does, and gives the keyboard to
  the one it shows. Claude Code binds Ctrl+H to no command of its own: in the terminal it was a Backspace.
- **Ctrl+V pastes in a session's terminal,** as Ctrl+Shift+V does, and as in every other Windows program: in Claude
  Code on Windows it did nothing. An image on the clipboard with no text, such as a screenshot, is pasted too: Cloyd
  saves it as a PNG in `%LOCALAPPDATA%\Cloyd\pasted-images`, kept a week, and pastes its file's path alone, which
  Claude Code takes for the image and shows as `[Image #1]`. Your clipboard is left as it was. In a shell, the path is
  pasted as text.
- **Ctrl+Tab selects a window's next tab, and Ctrl+Shift+Tab the one before,** as in a browser, from the last tab to
  the first and back, also while a terminal has the keyboard.
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
