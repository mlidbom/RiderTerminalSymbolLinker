# Changelog

All notable changes to Cloyd are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0]

### Added
- **Cloyd records unhandled managed exceptions before it exits.** Each failure gets its own file in
  `%LOCALAPPDATA%\Cloyd\crashes`, with the UTC timestamp, build, process and managed thread IDs,
  and the full exception and stack trace. A sandbox writes reports inside its own Cloyd folder.
- **Search the Projects Dashboard tree.** Click the magnifier or press Ctrl+Shift+F, then type words
  from a project, worktree, branch or session name. Matching rows appear under their headings, including
  in collapsed groups. An empty search closes when you click elsewhere or move focus away. A nonempty
  search stays active. Escape clears it and brings back the previous expansion. The clock beside the
  magnifier opens history for all projects.
- **Right-click a dashboard worktree to open Git Extensions.** Choose **Browse** for its revision graph
  or **Commit** for its commit dialog. Both open in the checkout you clicked.
- **Combine dashboard status icons above the tree.** Pinned comes first in the session switches. Each
  strip ends with **A**, which temporarily includes all its sessions or statuses and remembers the
  individual switches. While **A** is on, boxes mark the selected choices. Click an individual switch
  to change it and leave **A**. An empty strip selects **A** automatically. Worktree switches cycle
  **On → Off → Unset**. Unset follows the project's choice. **AG** shows all group headings.
- **History and Projects Dashboard share a session list.** Click a stored conversation to read it and choose
  **Resume session** to run it. The dashboard clock searches all projects; each project and worktree clock
  searches its own sessions, including those the sidebar filters hide.
- **Choose which sessions to include at each level.** The three icons include open project tabs, pinned
  sessions, and sessions outside project tabs. A match on any enabled icon is enough. Each individual
  switch inherits from its parent until changed locally. Right-click a local switch and choose **Use parent
  setting** to restore inheritance. The gear reveals hidden group headings for editing. The status selector
  narrows the resulting list to running, resumable, working, waiting, idle or failed sessions.
- **Worktree branch icons show Git status.** Green means all commits are in the local integration branch,
  purple means some are still outside it, and an amber dot marks uncommitted changes. On the integration
  branch itself the colour compares against its upstream. ↑ marks outgoing commits and ↓ incoming commits.
  Hover for counts. Cloyd reads local Git state without fetching.
- **Pin sessions in Projects Dashboard and choose Only pinned above the tabs.** The filter shows each
  pinned session under its worktree and project, hiding groups with no pins. Pins and the filter survive
  reopening the dashboard. Filtering leaves the current chat or terminal open.
- **Projects Dashboard lists the projects trusted in Claude Code's configuration and their Git worktrees,**
  including projects with no running sessions or open project windows. Recorded projects inherit trust from
  an accepted parent folder. Sessions appear under the worktree where their agent is running, including when
  the host's shell originally started in the main checkout.
- **Projects Dashboard arranges sessions as vertical tabs grouped by project and worktree.** Every registered
  worktree of a displayed Git repository is available, including empty checkouts and checkouts on another drive.
  A worktree’s **+** starts a session there. Drag projects, worktrees or tabs to reorder siblings. Click either
  group heading to collapse it. Its active tab stays visible and keeps its chat or terminal open. Collapsed
  headings count the sessions working or waiting. Cloyd remembers group expansion and tab order on reopening.
- **Coordinators can start a named Claude Code or Codex helper in a Cloyd tab.** The helper runs in the requested
  worktree and the existing tab stays selected. A repeated launch returns the original session's address.
- **A grey star marks an idle Claude Code tab and a lighter grey dot marks an idle Codex tab.** During a turn,
  Claude's star spins in orange and the Codex dot pulses in the same sky blue in both themes, growing to the same diameter as Claude's spinner.
  Codex Chat shows the same pulse above its input box.
  The title and close button stay in place. A grey star still marks a Claude Code session with background work.
  A tab adopted at a plain shell prompt leaves the mark empty until Cloyd identifies its agent.
  Projects Dashboard shows the same spinner or pulsing dot while a session works.
- **Projects Dashboard shows the sessions of every project in one window.** Open it at the far right of a
  project window's tab strip. Select a session to read its chat, write or send a message, or bring its
  terminal here with its scrollback. The project tab and dashboard share the same draft, including
  pasted text and images. The tree marks projects and sessions waiting for you and includes projects
  whose windows are closed. Start sessions with a project's **+**, end them with **×**, and open the session
  history with **Ctrl+Alt+Shift+Y**. Sessions running outside a host can be read when their chat is available.
- **Drag a tab header to rearrange sessions in a project window.** A marker shows where the tab will land,
  including on a wrapped row. Release to move it or press Escape to cancel. The window remembers the new
  order when it reopens, and Ctrl+Tab follows that order.
- **Codex Chat shows live progress above the input box and the configured status line below it.** While Codex
  works, a pulsing dot accompanies its activity, elapsed time and command count. The latest active tool has its own line,
  with its input and elapsed time. Completed turns show how long they took and when they finished, including
  in saved conversations when Codex recorded the times.
  The footer wraps to fit the window. It shows the model and effort, directory, context used,
  weekly allowance used and token counters in the order selected in Codex. As in Claude Chat, weekly usage
  turns yellow above 75% and bold red at 85%. New chats can show the account's usage before their first prompt.
  The label reads `week ?` while a reading is unavailable.
  Available permission settings and running background terminals appear beneath it. Hover over the terminal count to see their commands. The tab
  identifies the session by name, and Peers can read its ID in Terminal.
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
- **Both chats mark the end of a turn with semibold text and a matching thin divider.** The timing sits close to the reply,
  with the divider just beneath it. Both are near-black in the light theme and light in the dark theme. A line such
  as “Worked for 21s · done 14:37” uses the time alone today, the weekday in the last week and the date before that.
  Claude Code omits the timing line for interrupted turns.
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
  "Codex", with a pulsing dot while Codex works. A tab whose host runs Claude Code and then Codex, or the other way round,
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
- **Tabbing out of Projects Dashboard search keeps Cloyd running.** An empty search now closes after
  keyboard focus finishes moving. Reopening search, typing, and using Tab or Shift+Tab keeps the query.
- **The worktree context menu is visible when you right-click.** Open **Git Extensions** to reach
  **Browse** and **Commit** for the clicked checkout.
- **The dashboard keeps the selected session when another terminal takes over its host.** Chat stays open
  until the host actually exits. The terminal can be brought back on request.
- **Busy sessions keep running after Cloyd restores their tabs.** A restored tab first attaches when its
  terminal is laid out. Previously, output from a never-selected tab could fill its pipe and stall the session.
- **Moving a tab keeps its terminal connected to the same session.** You can continue typing at the same
  prompt after a drag or a tab switch. Previously a Windows command prompt could appear over the session
  and take keyboard input.
- **Screenshot links in replies open with one click.** Both chats open local images in the Windows image viewer
  and web links in the browser. A link to a missing image shows its path in the chat.
- **Opening a transcript row keeps its heading in place, even at the bottom of the chat.** Tool calls and peer
  messages open beneath the heading you clicked. A scrollbar drag can reach the bottom and move back up in
  one gesture. Release at the bottom to follow new replies, or release higher up to keep reading there.

- **Codex Chat restores an unconsumed steer.** If the turn ends before Codex records the message, its original
  text and pasted images return to the editor ahead of newer typing. Chat remembers pending steers when a tab reopens.
  A late delivery confirmation clears an untouched copy returned after a transport error. Newer edits stay in
  the editor. If history cannot settle delivery, you can copy the pending input into the editor yourself.
  Chat refuses to steer through an older hub without message receipts and keeps the draft. Terminal is available
  during that upgrade window. Press Send to try again.
- **Both chats distinguish your conversation from messages between agents.** Your messages appear in gray bubbles on
  the right and the session's replies in blue on the left. Incoming peer messages are violet. Outgoing peer messages
  are teal with an outgoing arrow. Both start folded in a borderless, single-line bubble showing the peer's name
  and protocol, such as `cloyd-gpt · Peers`. Open a message to read or
  copy it and see any delivery details. Ordinary tool calls keep their compact expandable rows.
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
