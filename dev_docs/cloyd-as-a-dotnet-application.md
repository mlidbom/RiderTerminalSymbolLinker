# Cloyd as a .NET application, in Avalonia, beside the IDEs

## Status

**Magnus's direction, 2026-11-20. The spike is run, and step one is built and deployed** (see "Step one, as it's
built"): since 14:22 that day his IDEs run Claude Sessions 0.2.0, and Cloyd shows his sessions, a window per project.

- **Agreed (Magnus, 2026-11-20):**
  - the direction below, and the calls under "How it's to be built";
  - the input box as plain as Avalonia's editor gives it;
  - one home, later, for the Avalonia extensions the apps share;
  - a Cloyd window per project, "for now";
  - the terminals first, and then all the Kotlin UI goes (under "How it's to be built");
  - the desktop app's Code tab as the chat's specification, over his own earlier wishes (see "The chat, when it comes
    back: the desktop app is its specification").
- **The spike, run on 2026-11-20 on his go,** found the TUI's host and nothing against the port: see "What the spike
  found".
- **Started on Magnus's go, 2026-11-20:** "Yes :)" to step one, built in `Cloyd/` from Rider.

## The direction

Magnus, 2026-11-20: "I think the big mistake we are making is assuming that cloyd as a whole should stay Java." In his
words:

- "Cloyd should become almost all .NET. A .NET UI using Avalonia."
- "The Jetbrains plugin part shrinks to only just what we need to communicate with the IDE. And much of that is already
  handled by the Claude MCP in the IDE. Though replacing this with something better is apt to be a good idea, the plugin
  that MCP comes with is all but abandoned in practice."
- "Cloyd can be a separate window that lives besides the Jetbrains IDE's." Putting the .NET UI inside Rider's windows,
  as NCrunch does, "can wait if at all problematic".
- "This means that cloyd is essentially the UI for all of tooling, rather than a Jetbrains specific thing."
- "This gives us all the fantastic tooling, experience, and processes we have polished in .NET in one stroke. At the
  cost of porting the Cloyd UI to .NET."
- And: "JAStudio, and Deskmancer are also both built on Avalonia. Virtually all our UI code is Avalonia and .NET".

## Why it's the right direction

- **Claude Code left the IDE before this was decided.** The hub owns everything about Claude Code, and since session
  hosts the sessions' consoles run outside the IDE (`claude-sessions.md`, "Who provides what" and "Sessions that
  outlive the IDE"). The plugin is already a UI over the hub's HTTP API.
- **Most of what makes Claude Sessions costly to build comes from living in the IDE,** and goes:
  - surviving a plugin reload, and the leak each reload leaves;
  - the unload hook on the IDE's UI thread, the EDT, and every coroutine scope it waits for;
  - the reworked terminal's experimental API, and Jewel below 1.0;
  - the Plugin Verifier, and Java 21 bytecode;
  - the Kotlin copy of the hub's contract, guarded only by a test against the hub's samples;
  - the .NET sidecar per IDE that Tessaging would need (below);
  - a sandbox Rider of 4.6 GB for every specification run;
  - deploying a change through a reload or an IDE restart.
- **What .NET gives:** the hub's own C# types (`~/.claude/src/ClaudeTooling.Client`), Tessaging directly,
  Compze.Threading, Magnus's language, and the code standards and their tools, Royder's included.
- **All our other UI is Avalonia and .NET,** with its specifications drawn on Avalonia's headless platform, in process:
  MachineStallViewer in `~/.claude/src` (Avalonia 12.1.5, from `~/.claude`'s `Directory.Packages.props`), JAStudio, and
  Deskmancer. JAStudio and Deskmancer are Tessaging endpoints already.
- **The size of the port,** in Kotlin lines on 2026-11-20 (Claude Sessions, about 7,500 in all):

  | Package | Lines | In the .NET Cloyd |
  |---|---|---|
  | `chat` | 3,100 | rewritten, to the desktop app's Code tab as its specification (see "The chat, when it comes back"). `claude-sessions.md`, "The chat view", holds Magnus's earlier wishes, which give way to the app's. |
  | `hub` | 1,100 | mostly replaced by the hub's own C# types |
  | `observation` | 1,000 | replaced by specifications on Avalonia's headless platform |
  | `history` | 460 | ported |
  | `api` | 390 | stays in the plugin |
  | `claudecode` | 340 | mostly the hub's already |
  | `saving` | 200 | most of it goes: the window doesn't die with the IDE |
  | `windows` | 180 | stays in the plugin |
  | `newsession`, `sessionhosts` | 200 | ported |

  The symbol linker, 1,570 lines, stays in the plugin.
- **Why not inside Rider's windows now:** NCrunch sends its WinForms and WPF UI into Rider through shared memory, its
  "wormhole", on Windows only. Its forum reports freezes, focus stealing, and padding and DPI quirks
  ([NCrunch V5](https://blog.ncrunch.net/post/NCrunch-V5-Now-Available.aspx),
  [its developer on the padding](https://forum.ncrunch.net/yaf_postst3643_Unwanted-padding-of-windows-in-Rider.aspx)),
  as Magnus has found in use.

## How it's to be built

Claude's calls (.NET-cloyd, the session in this repository), which Magnus OK'd on 2026-11-20:

- **The JetBrains plugin keeps only what needs the IDE:**
  - the symbol linker's links in Rider's terminal;
  - the local API's `GET /windows` and `POST /restart` with `beforeItStartsAgain`, for the hub's IDE restart, and
    showing a session;
  - putting each project window back on its virtual desktop after a restart: Windows' documented
    `IVirtualDesktopManager` moves only the calling process's own windows;
  - the JetBrains side of a click on one of Cloyd's links: navigating in the right IDE, and bringing its window forward
    (Magnus: "Which the plugin will handle");
  - later, perhaps, what Claude Code's own JetBrains plugin does for `/ide`, which Magnus finds all but abandoned.

  That is the merge of Claude Sessions and the symbol linker decided on 2026-11-19 (`claude-sessions.md`, at its top),
  without the UI.
- **The .NET application owns the UI:** the sessions it shows, the chat's rendering, the drafts, the input box, the
  TUI's host, and finding the links in what it shows: file paths, URLs, and C# symbols through Royder.
- **It starts on the hub's HTTP API as it is,** with no change to the hub and no wait for Compze (tessaging-in-tooling).
  Moving onto Tessaging is then .NET to .NET, the case Tessaging already serves, a flow at a time.
- **Only .NET joins Tessaging.** The application reaches each IDE through the plugin's local API, so no sidecar is
  needed: java-tessaging's simpler way to bridge to a thin plugin.
- **The hub and the application stay separate processes.** The hub is republished several times a day, one per Claude
  Code folder, and has no window: a UI in it would restart at every publish. So the hub's API keeps its rule that
  changes only add, as the application runs on through the hub's publishes.
- **The first step is the terminals alone, and then all the Kotlin UI goes** (Magnus, 2026-11-20, overriding the earlier
  call that the Kotlin chat stays until the .NET one is better): "once Cloyd in .NET can even just show the terminals
  and nothing else, no UI, get rid of all the UI in java". He worked with terminal tabs alone until a few days before.
  - **Why it's simpler:** no session is ever shown by two UIs, so attaching never takes a TUI away from a tab. No parity
    has to be reached before the switch, and the Kotlin side of it is deletion.
  - **Why it's cheap:** the spike has most of it: the attachment, the terminal and the window.
  - **Clickable file paths and C# symbols come right after it** (Magnus, 2026-11-20: "Agreed"), so step one stays
    quick. Until then, URLs and the links Claude Code writes itself (OSC 8) are what is clickable in Cloyd's terminal.
  - **The chat comes back afterwards,** built in .NET, to the desktop app as its specification (see "The chat, when
    it comes back: the desktop app is its specification").
  - **Built in `Cloyd/`,** a solution of its own in this repository, which can move to a repository of its own.
- **The input box is Avalonia's editor, AvaloniaEdit, unpolished,** with standard Windows text editing. Magnus: "None
  of the claude UI's have a super competent input box, very few chats do. Any polishing at all of this can wait."
- **The helper that runs a deploy between an IDE's exit and its start** can move to the .NET side, which outlives the
  IDE (tessaging-in-tooling): simpler than a helper the plugin leaves behind.

## Step one, as it's built

**Built (3cbb526, 73cd255, 84b7525, 2496008):**

- **The switch, Claude Sessions 0.2.0, deployed on Magnus's go, 2026-11-20,** with `everyday deploy
  --restarting-the-ides`: IntelliJ IDEA was back in 15 s and Rider, with three solutions, in 38 s, and Cloyd showed all
  four projects' windows, with the sessions that ran on in their hosts. Its application, started by IntelliJ IDEA's
  Claude Sessions, runs in no job, where both IDEs run in one: it broke away.
  - **The plugin has no UI:** as a project opens, it runs `Cloyd.exe --project <the project's folder>` from `bin` in
    Cloyd's folder, and a notification says why when Cloyd isn't installed or doesn't show the window
    (`claude-sessions.md`, "Showing each project in Cloyd"). It keeps the local API's `GET /windows` and
    `POST /restart`, and puts the IDE's windows back on their virtual desktops after a restart. `GET /sessions` and
    `POST /sessions/show` went with the tabs.
  - **The window comes in front of the IDE's:** the command lets the application bring it there (`TheForeground`,
    Windows' `AllowSetForegroundWindow`), and Avalonia's `Show` and `Activate` call `SetForegroundWindow`. Not specified:
    Windows lets a process pass the right on only while it may take the foreground itself, as the IDE in front and what
    it starts may, and a specification that took the foreground would take it from Magnus.
  - **Every `everyday deploy` puts Cloyd in first:** published in Release into `%LOCALAPPDATA%\Cloyd\bin`, moving the
    files in use aside. An application that runs runs on with the build it has until its last window closes.
  - **Specified in the sandbox Rider,** which has a Cloyd of its own (`development-environment.md`, "The sandbox
    Rider"): Claude Sessions, starting with a project open, has Cloyd start and show the project's window; Cloyd's
    application runs on through an IDE restart, as the same process; three solutions opening each show in Cloyd.
  - **Not seen in the sandbox: breaking away from the IDE's job.** The sandbox Rider runs in no job, as the tools start
    it, where Magnus's Rider and IntelliJ IDEA run in one, as explorer starts them (2026-11-20). Cloyd's own
    specification of a job that ends everything in it as it closes covers the breakaway, and Magnus's Cloyd showed it
    (above).
- **One Cloyd application, with every project's window** (`Cloyd.LocalApi`):
  - **`Cloyd.exe --project <folder>` is a command:** it hands the project to the Cloyd application, and exits once the
    project's window shows, 0.16 s when the application runs, 0.84 s when it starts it. Opening a project's window
    again brings it forward.
  - **The application, `Cloyd.exe --application <Cloyd's folder>`,** is the one process with every project's window
    (`ProjectWindows`). It starts with no window, and ends as its last window closes.
  - **Its local API** (`CloydsLocalApi`), on Kestrel on a free loopback port, is announced in
    `%LOCALAPPDATA%\Cloyd\local-api.json` as the plugin's and the hub's are: `GET /windows`, and `POST /windows/show`
    with `{"project": <folder>}`. Every request carries the announcement's token, and names the API's own `Host`.
  - **The command finds the application, or starts it,** in a Compze `IMutex` critical section, so that IDEs opening
    several projects at once start one application.
  - **It starts the application breaking away from its jobs,** with `CREATE_BREAKAWAY_FROM_JOB`, where they let it,
    else as the hub is started: an IDE's job may end what is in it as the IDE exits. Rider's job's limits couldn't be
    read: the process that holds it can't be opened without elevation.
  - **Cloyd's folder,** `CloydsFolder`: `%LOCALAPPDATA%\Cloyd`, or what `CLOYD_FOLDER` names, as for a Cloyd the
    specifications run apart from Magnus's.
- **A window per project,** with a tab per session host of the project. Each tab shows the host's terminal in
  Iciclecreek's control, attached in process (`AttachmentToASessionHost`), and is titled with the name of the session
  the hub says runs in its host.
- **Shift+Enter types a new line** in Claude Code's input box, as a line feed, which Claude Code takes as Ctrl+J
  (`TerminalOfASessionHost`): Claude Code ignores the Shift on Enter in a Windows console, and sent the message
  (Magnus, 2026-11-20, his first hour in Cloyd).
- **The hosts it shows as it opens:** each of the project's hosts that no attach client shows. The project is told by
  the host's label (`SessionHostLabel`): Cloyd's own, `{"startedBy": "cloyd", "project": …}`, or Claude Sessions',
  `{"ideConfigFolder": …, "project": …}`, so that it takes over the plugin's sessions once no IDE's tab shows them. A
  host an IDE's tab shows is left to the tab: until the switch, Cloyd takes no session from a tab.
- **The +** starts a new session, named or not, in a new host in the project's folder: `pwsh.exe` where it is on the
  `PATH`, else `powershell.exe`, with `TERM` and `COLORTERM` on top of a new process's environment.
- **Closing a tab ends its session;** closing the window only lets go. A tab closes when its host lets it go: another
  attach client took the session over, or its shell exited.
- **Saving the tabs, and bringing them back** (`Cloyd.Saving`), as the plugin does (`claude-sessions.md`, "Saving and
  bringing back"):
  - **Saved:** each tab's host, the directory its shell started in, and its `LatestSession`, the session that runs in
    it or ran in it last; in their order, with the selected tab's host. Every 2 s, and as the window closes, to
    `%LOCALAPPDATA%\Cloyd\project-windows\<the project's folder as a name>.json`. Saving starts only once the window
    has brought the saved tabs back, so a window whose bringing back failed, as with no hub, leaves the save as it was
    for the next one, and saves nothing of its own: its foot tells the failure.
  - **Brought back as the window opens, one after another:** a tab whose host runs is attached to it again, unless an
    attach client shows it. A tab whose host is gone, as after a reboot, has its session resumed with
    `claude --resume <id>`, through the hub's `sessionToResume`, in a new host in the same directory: about 1 s a tab
    in the sandbox, the hub's wait for the shell's prompt. Left out: a tab whose host ran no Claude Code, and one whose
    session's transcript is gone from the Claude Code folder the hub's announcement names (`claudeCodeFolder`).
  - **Then the project's other hosts** that no attach client shows, as before, and the selected tab is selected again.
- **Specified** on Avalonia's headless platform, against a hub of the specifications' own, the tooling's
  `hub sandbox start` (`Cloyd.Specifications/TheSandbox`), whose sessions run the tooling owner's stand-in for Claude
  Code. They run in about 3 minutes, touch no session of Magnus's, and draw each state, and each wait in vain, to
  `bin/.../RenderedWindows/`. One scenario runs the real `Cloyd.exe` in a job that ends everything in it as it closes,
  as an IDE's may: its application's window shows on the desktop for a few seconds.

**Next, in this order:**

1. **A notification's click shows the session in Cloyd:** the hub's side is the tooling owner's.
2. **Each project's window on its IDE window's virtual desktop** (Magnus, 2026-11-20: "do"): after the switch's restart
   all four opened on the desktop he was on. Windows' documented virtual-desktop API tells any window's desktop, and
   moves the calling process's own windows; the IDE's local API lists its project windows.
3. **Clickable file paths and C# symbols in Cloyd's terminal** (Magnus, 2026-11-20, keeping the order: the links before
   the chat, which needs the same link finder).

Then the chat, to the desktop app (below).

## The chat, when it comes back: the desktop app is its specification

**Magnus, 2026-11-20:** "Once we start building the real .NET UI I think we should copy the look, feel, and
functionality of the claude desktop app by default. So I figure that once we start that, the desktop app becomes your
spec for how the chat view should look and behave." Only the transcript, how it looks and behaves: "Not the whole
application." Close, not exact: "Exactly matching look is not required though. Don't put in a ton of effort to try to
look exactly the same." And over his own earlier wishes: "Actually I think the desktop app UI is the spec for now, not
what I have said. I think it solves everything that I was trying to solve and does so well. Record any conflicts, but
build to the app as the spec." Why: one reference holds all the app's small decisions at once, where Magnus asking for
one thing at a time is slow.

- **The specification:** the Claude desktop app's Code tab, as it shows one Claude Code conversation: how it looks, and
  how it behaves when clicked, hovered and scrolled, and while Claude writes. Not its sidebar, nor its input box, nor the
  rest of the app: Cloyd's input box stays AvaloniaEdit, unpolished (under "How it's to be built").
- **It overrides Magnus's earlier wishes for the Kotlin chat** (`claude-sessions.md`, "The transcript pane"). Where they
  differ, build as the app does, and record the conflict below, for him.
- **What the app doesn't have is no conflict:** clickable file paths and C# symbols (under "How it's to be built") are
  Cloyd's own, and stay.
- **Close, not exact:** the app's layout, structure and behaviour; its spacing, sizes and colours near enough. Its fonts
  and icons are Anthropic's: similar ones, not copies.

### How it's built to the app

- **Looking at the app takes nothing of Magnus's screen:** `ClaudeTooling.exe window capture --process <the app's
  process id> <png>` draws its window at its full size in pixels, on another virtual desktop or covered, without
  activating it. Checked on 2026-11-20: 2900 × 1878 pixels, its text and code legible. Not while it's minimized.
- **The same conversation in both:** a Code session of the app writes an ordinary Claude Code transcript to
  `~/.claude/projects`, as the CLI does (checked 2026-11-20), so Cloyd draws a session the app ran. The app also offers
  to import the CLI's sessions.
- **What the app shows and does, found on 2026-11-20, with its pictures and the session they show:**
  `cloyd-chat-specification.md`.
- **A reference, captured once for each part:** the app showing a conversation made for the purpose, so that no private
  conversation lands in the repository: tool calls one by one and in runs, a failed call, thinking, Claude's markdown
  (headings, lists, tables, code), a diff, an interruption, a compaction, and a conversation long enough to scroll. Kept
  beside the specifications.
- **Then working towards it takes no desktop:** the specifications draw each state to an image on Avalonia's headless
  platform (`RenderedWindows/`). Cloyd draws the same conversation, compared with the reference, in seconds.
- **Behaviour takes driving the app, on Magnus's screen, when he says he's ready.** The sessions needn't be short
  (Magnus). Windows-MCP drives the real desktop: the real mouse and keys, and the window in front. So, as far as is
  known, does Claude's own computer use, which acts on pictures of the screen. With the app on the current virtual
  desktop, its UI Automation tree names its buttons and texts, read from PowerShell without touching it; on another
  virtual desktop, it found no window.
- **Chromium's remote debugging, which would take no screen, the app refuses:** started with
  `--remote-debugging-port` or `--remote-debugging-pipe`, through its alias `claude-desktop.exe`, it quits within a
  second, where an unknown switch leaves it running (2026-11-20, app 6.6395). Getting around that, as through Node's
  `--inspect`, would undo what the app does on purpose, and wasn't tried.

### Conflicts with Magnus's earlier wishes

Each as it is found: what the app does, what he asked for and where, and that the app's was built. Kept in
`cloyd-chat-specification.md`, "Conflicts with Magnus's earlier wishes".

## What's still open

- **The TUI needs a host. Settled by the spike: Iciclecreek.Avalonia.Terminal** (see "What the spike found"). Slash
  commands, permission prompts, pickers and dialogs are drawn only by Claude Code's TUI, and a tab switches to it
  whenever one opens. In the IDE, its terminal hosts the TUI. The candidates as they stood before the spike:
  - **A native Avalonia terminal control:**
    [Iciclecreek.Avalonia.Terminal](https://vat.pages.dev/tomlm/Iciclecreek.Avalonia.Terminal), on XTerm.NET, for
    Avalonia 12, with releases into July 2026;
    [AgentSpace.Avalonia.XTerm](https://feed.nuget.org/packages/AgentSpace.Avalonia.XTerm/);
    [AvaloniaTerminal](https://packages.nuget.org/packages/AvaloniaTerminal/).
  - **xterm.js, VS Code's terminal emulator, in Avalonia's `NativeWebView`,** which JAStudio uses, with traps it has
    written down: the headless platform can't render a web view, taking one out of the visual tree destroys it,
    Avalonia doesn't see the pointer over it, and keyboard focus needs handling of its own. JAStudio is moving its
    pages off the web view, so it's the fallback here, not the first choice.
  - **Windows Terminal,** running `ClaudeTooling.SessionHost.exe attach <id>`: no work at all, and a second window for
    each session.
- **How Cloyd's windows relate to the IDEs' windows: one per project, "for now"** (Magnus, 2026-11-20), across his 3-8
  IDEs on their virtual desktops. The alternatives were one per virtual desktop, or one for everything. Cloyd can move
  its own windows with the documented API; Deskmancer, Magnus's window-placement app, could place them too. Whether each
  window also lists every session on the machine is open.
- **Publishing:** a .NET application and a plugin. Magnus, 2026-11-19: "How, and even if, we publish this is a
  question for the future."
- **Where the hub's contract types live: settled.** In `~/.claude/src/ClaudeTooling.Hub.LocalApi.Contract`, published
  2026-11-20, which Cloyd compiles in.

## The spike

Throwaway, on the branch `spike/dotnet-cloyd` (commit b39fef7, `spikes/CloydSpike`, with a README on running it), to
settle the TUI's host and see how fast the porting goes:

1. Each candidate for the TUI's host above, attached to a session host running the real Claude Code's fullscreen TUI:
   resizing, the mouse wheel, pasting, colours, the alternate screen.
2. An AvaloniaEdit input box that sends through the hub.
3. One session's conversation, from the hub, rendered as markdown.
4. One specification on Avalonia's headless platform.

### What the spike found (2026-11-20, Claude Code 2.1.329)

**Go, with Iciclecreek.Avalonia.Terminal as the TUI's host.** Each of the four parts worked, and nothing found argues
against the port.

**How it was tried.** The window attaches to a session host in process, over the host's own protocol: the hub's types,
compiled in from `~/.claude/src/ClaudeTooling.SessionHost.Protocol`, with no console of its own between the host and the
terminal control. The attachment is 135 lines. The spike started a throwaway session host of its own, with the real
Claude Code on Haiku in it. The host carried a label of its own, so that no IDE's tab claimed it, and the spike attached
to no other host, since attaching takes a session over from its tab. A probe on Avalonia's headless platform, with
nothing on the desktop, drove each candidate with keys, pastes, resizes and the mouse wheel. At each step it drew the
control to an image, beside what the session's own console showed (`ClaudeTooling.exe typing screen`), and it logged
the bytes each control sent.

**The TUI's host:**

- **Iciclecreek.Avalonia.Terminal 4.1.4: the one.**
  - **Drawing:** Claude Code's fullscreen TUI as Claude Code draws it, its block-character logo, colours, bold, tables'
    box drawing, a dialog, the slash-command picker and the status line included.
  - **Keys:** it speaks the console's win32 input mode, which conhost asks every terminal for and the session host
    replays, so each key reaches Claude Code exactly. Two Escapes cleared the input box. Ctrl+Shift+V pasted three lines
    as a bracketed paste. Ctrl+V reached Claude Code as its own Ctrl+V, the image paste. It also sent focus events.
  - **Mouse and size:** the wheel went to Claude Code as mouse reports. The session's own console followed each resize:
    144 by 46, 180 by 55, 107 by 36.
  - **Fitting it in:** it plugs in through `AttachConnection(IPtyConnection)` in 43 lines, and draws on the headless
    platform, so an agent can drive and see it with no desktop.
- **AgentSpace.Avalonia.XTerm 0.1.7: out.** It draws as well, the logo with gaps, but sends plain VT, and a lone Escape
  byte doesn't reach Claude Code as the Escape key: two presses left the box as it was. Escape interrupts a turn.
- **AvaloniaTerminal 1.0.0: out.** It is built against XTerm.NET 1.0, and fails in a process that has Iciclecreek's
  XTerm.NET 3.0 (`MissingFieldException`).
- **xterm.js in `NativeWebView`: works, the fallback.** It drew the TUI in a real window, once the application had a
  manifest listing the Windows versions it supports, as `JAStudio.StandaloneHost`'s has: without one, Avalonia can't
  create the web view's native window, and the application crashed. It can't be drawn headless, and its keys weren't
  tried, as that takes Magnus's desktop.
- **Windows Terminal, through the attach client: not tried,** as it opens a window on Magnus's desktop. The window has
  a button for it, and the session hosts' own spike found the attach client works in a console.
- **Not seen:**
  - the wheel's effect, as the conversation was too short to scroll;
  - text selection and copying, links (OSC 8), input methods, and scaling across monitors.
  - Shift+Enter acted as Enter in every candidate: Claude Code ignores Shift on Enter in a Windows console, as known from
    the session hosts' spike, even with the Shift that win32 input mode carries.

**The rest:**

- **The input box:** AvaloniaEdit as it comes. Enter raises the send, and Shift+Enter is a new line (specified).
  Sending through the hub worked; Haiku answered.
- **The conversation:** LiveMarkdown.Avalonia renders what the hub sends: a table, a list, bold, and C# with highlighting
  and a copy button. Tool calls show folded, with their input and result inside. This session's 164 messages caught up
  and laid out in 3 s, unvirtualized: a long conversation needs a virtualizing list.
- **Specifications:** five, on the headless platform, in 0.7 s. One feeds the conversation view the hub's own contract
  sample stream, and draws it to an image.
- **Porting speed:** the window, the hub's client, the attachment, three candidates and the chat, about 900 lines, plus
  a 235-line probe and the specifications, in about an hour, with the TUI tested against the real Claude Code by the
  agent alone.

**What the real Cloyd must do that the spike showed:**

- **Show a session host's TUI before the hub lists its session.** A new session can open with a dialog, such as
  "Allow external CLAUDE.md file imports?" in a new folder in a repository, and the hub lists no running session until
  Claude Code is past it.
- **Decide which sessions' TUIs it attaches to.** Attaching takes a session over from its IDE tab. While the Kotlin
  plugin still shows the TUI, Cloyd showing a session the tab shows takes the TUI away from the tab.

## What it means for the Tessaging work

On 2026-11-20, before this direction, four sessions designed how Cloyd would take part in Tessaging: java-tessaging,
compze-rep, tessaging-in-tooling and tessinging-in-cloyd (`claude-sessions.md`, "Talking to the hub through Tessaging";
Compze's `src/Compze.Tessaging/dev_docs/TODO/java-participants-through-a-dotnet-sidecar.md`;
`~/.claude/dev_docs/tessaging-in-the-tooling.md`). All three that answered found nothing against the direction.
java-tessaging: "a mostly-.NET Cloyd removes the problem my work existed to solve."

- **Moot:**
  - the .NET sidecar per IDE, the Java port, and .NET hosted in the IDE's process;
  - calls from one IDE's Cloyd to another's, as there is one application;
  - pushing every conversation to every IDE's sidecar: the application is one subscriber, following the conversations
    it shows;
  - tessaging-in-tooling's step 2, the first Java flow, and its Compze changes 8 (Kotlin mirrors of the contract
    types) and 14 (subscriptions narrowed by content).
- **Changed: something still stands for each IDE.** The hub's IDE restart, a notification's click, Deskmancer's window
  placing and navigating from a link each need one particular IDE. With the plugin's local API, finding the IDEs stays
  an announcement file each, the per-process mechanism Tessaging was to replace: acceptable while the plugin is thin
  (java-tessaging).
- **Unchanged:** Compze's gaps, as they concern the tooling's processes, not Java: the hub's handover at a republish,
  addressing one endpoint among several, peers that depart and must be forgotten, live state as a snapshot plus its
  changes, deadlines and cancellation for long calls, a throwing tommand retried with no deduplication, and versions
  across separately built peers.

## To tell when the work starts

- **The tooling owner:** the bundle document's split, `~/.claude/dev_docs/the-claude-tooling-bundle.md`, gains a third
  part, the UI; the home of the hub's contract types; a notification's click (the hub's `NotifyingMagnus`) would ask the
  application to show a session, not the IDE. MachineStallViewer's publish folder was held open by Avalonia's telemetry
  collector, which `AVALONIA_TELEMETRY_OPTOUT=1` fixed.
- **tessaging-in-tooling:** rewords its open items to Magnus, dropping step 2 and change 8.

## Agreed for later: one home for the Avalonia extensions

Deskmancer has `Deskmancer.AvaloniaEX`: its window base class, single-open windows, and dispatcher and shutdown
helpers. JAStudio has its own, such as `OwnedWindowsExtensions` and `NativeKeyboardFocus` in `JAStudio.UI/Utils`, and
`WindowPlaceExtensions`. Once Cloyd exists, compare the three apps' extensions and give the ones they share one home
(Magnus, 2026-11-20: "Very good point. Agreed."). Not compared yet.
