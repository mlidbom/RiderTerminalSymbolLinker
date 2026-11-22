# Changelog

All notable changes to Cloyd are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0]

### Added
- **Codex Chat shows live progress above the input box and the configured status line below it.** While Codex
  works, a spinner shows its activity, elapsed time and command count. The latest active tool has its own line,
  with its input and elapsed time. Completed turns show how long they took and when they finished, including
  in saved conversations when Codex recorded the times.
  The footer wraps to fit the window. It shows the session, model and effort, directory, name, context used,
  weekly allowance left and token counters in the order selected in Codex. Available permission settings and
  running background terminals appear beneath it. Hover over the terminal count to see their commands.
- **Codex sessions open in Chat with a hub that supports it.** Chat and Terminal show the same session.
  Use **Ctrl+H** to switch between them. Chat shows your prompts, Codex’s replies as they arrive, and tool calls
  you can expand. **Enter** starts a new line. **Ctrl+Enter** sends a prompt or adds an instruction to the turn
  already running. **Stop** interrupts that turn. When Codex asks for an approval, Chat offers **Show the terminal**
  so you can answer there. If Cloyd loses the acknowledgement of a message, it puts the text back in the editor
  and tells you to check the conversation before sending again. If Terminal changes sessions, each Codex draft
  stays with the session it was written for, including when you close and reopen the project window.
  You can paste an image into a prompt or into a running turn. Open a tool call to see the command and its output,
  file changes, or tool arguments and results. A tab whose Codex has exited can still show its saved conversation.
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
    It goes once the conversation has it. It shows under the tool call under way, which it waits behind, and over the
    turn's line with the spinner.
  - **Ctrl+V keeps what you paste apart from what you type,** as Claude Code's own box does: a long paste reaches
    Claude as pasted. An image on the clipboard with no text is pasted as `[Image #1]`, with a thumbnail over the box
    that a click shows whole; Claude gets the image.
  - **A message Claude Code didn't get comes back into the box,** ahead of what you wrote since, with why over it in
    red: such as when Claude Code's own box in the terminal holds text, which the message would have mangled.
  - **A slash command,** such as `/compact`, switches the tab to its terminal, where Claude Code shows what it does.
  - **What the box holds comes back with the window,** after Cloyd restarts, typed and pasted parts as they came in.
- **Claude Code's own status line under the chat's input box,** all of it, as the terminal shows it under Claude Code's
  box: your status line command's, such as "Opus 5.5 · xhigh · context 42% · week 100% · Fable 7% · €361.07", with the
  permission mode under it, such as "⏵⏵ bypass permissions on". It shows once Claude Code has given it to the hub,
  within a minute of its start, in its colours, such as a weekly limit nearly used up in bold red, each in a shade
  readable on the chat's ground in Windows' light theme and its dark.
- **What a session runs in the background, under the chat's input box and in its tab's header,** so that a session that
  waits on something isn't taken for one that is done:
  - **Under the permission mode, what runs, counted as Claude Code counts it beside its box,** such as "1 shell" or
    "2 shells · 1 monitor · 1 scheduled prompt", whether Claude works or not. Its tip lists each, with what it does in
    Claude's words and a shell's command. The prompts that `/loop` and Claude's wake-ups schedule count too.
  - **In the tab's header, a grey ✻ where the spinner turns while Claude works,** while Claude is idle and background
    work runs, its tip saying what runs.
  - **After Claude's last turn, in the line that ends it, what it left running,** as Claude Code writes it:
    "· 1 shell still running", or "1 shell, 1 monitor".
  - **As of the end of Claude's last turn,** when Claude Code tells it: a task that ends while Claude works stays listed
    until that turn ends. Until a turn has ended since the hub started, the line says only "A background shell or monitor
    runs", while Claude is idle.
- **Search the chat's conversation with Ctrl+F, as in a browser:** a find bar at the top right of the conversation, which
  searches as you type, case not mattering, from the top of what is in view, and says which match it shows of how many,
  such as "3 of 16". F3 or Enter goes to the next match, down the conversation, Shift+F3 or Shift+Enter back up, and
  Escape closes the bar. It finds what the whole conversation shows, however far up, in your prompts, Claude's text, the
  grey lines and the lines of tool calls, though not what is folded under them, such as a tool call's output. Each match
  is marked in yellow, the one shown in orange and scrolled to the middle of the view, which keeps it there until you
  scroll.
- **A grey line after each of Claude's turns in the chat, as Claude Code writes one in the terminal:** how long the turn
  took and when it ended, such as "Worked for 21s · done 14:37": the time alone today, with the weekday in the last week,
  with the date before that, as Windows formats them. A turn interrupted has none, as in the terminal.
- **Over the chat's input box while Claude works, what it does, as Claude Code's own UI shows it:**
  - **The tool call under way,** as Claude described it, and how long it has run, such as
    `Finding the repositories that use it · 5s`, with the first three lines of the command under it, as
    `└ $ ls /c/Dev`; a file's tool call with the file's path.
  - **Under it, the turn's line:** a spinner; what Claude does, waiting for the model, thinking, writing, writing a tool
    call such as `Writing the Write call…`, or running tools; how long the turn has taken; its output tokens; and how
    many commands it has run, such as `Thinking… (1m 12s · ↓ 1.2k tokens · 3 commands)`. Where Claude Code doesn't tell
    the hub of its turn, it says `Working…`, and counts the time from when Claude started working.
- **A tab whose Claude works shows Claude Code's spinner before its title,** so that you see which sessions work, in
  every tab of a window.
- **The tabs look like tabs:** on a strip a shade darker than the window, the selected tab in the window's own ground,
  joined to what it shows, as in a browser, with titles at 15 px where they were 24. The **+** and the history button,
  now a clock icon, are at the strip's start, before the tabs and as high as them, where they were in a row of their own
  above.
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
- **Codex sessions in the tabs,** on a machine where Codex is installed. The **+** offers Claude Code or Codex, Claude
  Code chosen, with the name box under them. It offers Codex once Claude tooling's hub can start Codex sessions, as
  from the tooling that hosts Codex on a machine where npm installed Codex, whether any Codex session runs or not; else
  Claude Code alone, with no choice to make. With an older hub that offers Codex terminals only, the tab keeps its
  terminal and Ctrl+H leaves it there. It is titled with the session's name, else
  "Codex", with the spinner while Codex works. A tab whose host runs Claude Code and then Codex, or the other way round,
  follows the one that runs.
  - **The history picker lists Codex sessions too,** in one list with Claude Code's, most recently used first, each
    marked **Claude Code** or **Codex**. A toggle for each, both on as the picker opens, lists its sessions or leaves
    them out. A Codex session is called by its name, else its first prompt, with its first prompt under its name.
    Choosing one does what choosing a Claude Code session does, resuming it with `codex resume`. Sessions `codex exec`
    ran aren't listed. From a hub that serves Claude Code alone, the picker shows neither marks nor toggles.
  - **A window keeps its Codex tabs,** as it keeps its Claude Code tabs: a tab's session is saved with its agent CLI, and
    one whose host is gone, as after a reboot, is resumed in a new host with `codex resume`, unless Codex no longer has
    the session, as after it was archived. Windows saved before read their sessions as Claude Code's.

### Fixed
- **Codex Chat and Claude Code Chat identify incoming peer messages.** A row names the sending session and previews
  its message. Open it to read or copy the full text. This also applies to saved conversations and to Peers channel
  messages received while Claude was working.
- **Moving the mouse over a session's terminal no longer types text into it.** In some Codex sessions each move typed
  characters such as `CO!CL!CI!` into Codex's input box. A program in the session had turned off a way of reporting the
  mouse that Windows' console can't read; Cloyd's terminal took that for turning off them all, and fell back to an older
  one the console can't read either, which it then typed in as text. Cloyd's terminal now ignores what a program asks of
  the ways the console can't read, and the mouse reaches Codex as the mouse, also in a tab shown for the first time.
- **Shift+Enter types a new line in Codex's input box,** as in Claude Code's: it typed nothing there, and what you
  typed next ran on in the same line. Codex starts a new line on Shift+Enter itself, so Cloyd now passes the key to it
  as pressed, and types Ctrl+J for Claude Code alone.
- **Your prompts show at the right of the chat's column,** as in the desktop app, lined up with the input box: in a
  conversation of prompts alone, as a new session's is at first, they showed in the middle.
- **Shift+Enter types a new line in Claude Code's input box,** where it sent the message: in a Windows console Claude
  Code ignores the Shift on Enter, so Cloyd types Ctrl+J, Claude Code's key for a new line.
- **A long conversation no longer hangs Cloyd.** Opened on a long session, the chat flickered and Cloyd stopped
  answering. The chat now draws the conversation's last 100 messages and tool calls, and 100 more each time you scroll
  up near its top, keeping in place what you were looking at.
- **A tab that closes no longer keeps a processor core busy.** Each terminal whose session ended in it, as its tab
  closed or its host let it go, kept a core busy for 30 seconds.
