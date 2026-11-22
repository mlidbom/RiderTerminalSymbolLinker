# Cloyd as a .NET application, in Avalonia, beside the IDEs

[Projects Dashboard](cloyd-every-session-in-one-window.md) describes the application-wide session state, how
both windows edit one draft, and how a terminal moves between a project tab and the dashboard.

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
  - **Clickable file paths and C# symbols come in the chat, not the terminal** (Magnus, 2026-11-20, see "Clickable
    file paths and C# symbols: the plan"). In Cloyd's terminal, URLs and the links Claude Code writes itself (OSC 8)
    are what is clickable: underlined under the pointer, and a Ctrl+click opens a web page's in the browser (below).
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
  - **A notification's click shows the session's tab:** `POST /sessions/show?session=<id>&process=<claude.exe's id>`,
    the contract the plugin's tabs answered until 0.2.0, selects the tab whose Claude Code runs as the process, else the
    one that runs the session or ran it last, and brings its window forward: 200 with the window, 404 when no open
    window has such a tab, 400 when it names neither. The process finds the tab after a `/clear`, the session one whose
    Claude Code exited to the shell. The hub, which got the click, is to let Cloyd take the foreground first.
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
- **Each project's window goes back to the virtual desktop it was on** (`ProjectWindow.Desktop`), saved with its tabs,
  and, when it has none saved, as on its first opening, to its IDE window's desktop. Magnus keeps each IDE window on a
  desktop of its own, and Cloyd's mostly beside it: after the switch's restart all four of Cloyd's opened on the desktop
  he was on. Then he kept one on another desktop than its IDE window's, beside Rider's solution of Cloyd, which the
  window following its IDE window's desktop undid at each restart. So since 2026-11-20 (Magnus's choice of the options
  put to him) a window follows neither afterwards: where he moves it, it stays, and one whose IDE window he moves he
  moves too.
  - **Where the window is:** each time it saves, every 2 s, it asks Windows which desktop it is on, and saves that. As
    it closes it saves the one it last saw, since Windows no longer knows it then.
  - **Where the IDE's window is,** for a window with no desktop saved: every 2 s until an IDE shows the project, the
    window asks the IDEs that announce their local APIs in `%LOCALAPPDATA%\ClaudeSessions\ides` (`TheIdes`), whose
    `GET /windows` names each project window's desktop. An IDE whose process doesn't run isn't asked: a crash leaves its
    file, and Windows takes 2 s to refuse a connection to its port.
  - **A desktop that is gone,** as when Magnus closed it: moving there fails, and the window says so at its foot, on
    the desktop it opened on, which it saves. Not seen yet: Windows' answer to it is a guess.
  - **Moving it:** Windows' documented `IVirtualDesktopManager`, which moves only the calling process's own windows
    (`NativeWindow`).
  - **An IDE opening the project no longer brings forward a window on another desktop:** Windows switches to the
    desktop of a window that comes to the foreground, so an IDE restarting, opening its projects again, would have taken
    Magnus to each one's desktop in turn. A notification's click still does, as is its purpose.
  - **Specified** with a stand-in IDE (`StandInIde`), which serves `GET /windows`: the desktop the window went to, the
    one it saved, and the one it goes back to. The move itself can't be specified on the headless platform, whose
    windows Windows doesn't know.
- **A Ctrl+click on a URL opens it in the browser** (`TerminalOfASessionHost`), as Iciclecreek's terminal leaves to its
  host: it raises `UrlClicked`, and "never opens it". Only `http` and `https`: a link Claude Code writes itself (OSC 8)
  can have any scheme, such as `file:` for a program. Until 2026-11-20 nothing handled the click, and it did nothing. Not
  specified, as the headless platform opens nothing.
- **Shift+Enter types a new line** in Claude Code's input box, as a line feed, which Claude Code takes as Ctrl+J
  (`TerminalOfASessionHost`): Claude Code ignores the Shift on Enter in a Windows console, and sent the message
  (Magnus, 2026-11-20, his first hour in Cloyd).
- **Ctrl+V pastes** (`TerminalOfASessionHost`), as Ctrl+Shift+V, Iciclecreek's key, does: in Magnus's Cloyd it did
  nothing (2026-11-21). Claude Code's documents bind its image paste (`chat:imagePaste`) to Alt+V on Windows, though the
  spike below saw Ctrl+V reach it as the image paste: Cloyd takes the key either way. Text
  on the clipboard goes through the control's own paste, bracketed once the program asked for that. An image with no
  text is saved as PNG by `PastedImages`, in `%LOCALAPPDATA%\Cloyd\pasted-images`, kept a week, and its path pasted
  alone, bracketed, which Claude Code takes for the image, as the hub hands one on (`claude-sessions.md`, "The chat
  view: what the spikes found"); in a shell, which asked for no bracketed pastes, the path goes as text. Not Alt+V for
  Claude Code to read the clipboard itself: that can't be specified, as the headless platform's clipboard is no
  Windows clipboard. Specified with the stand-in, whose input box shows `[Image #1]` only for an image file's path
  pasted alone. Not seen yet: Avalonia reading a real screenshot off Windows' clipboard.
- **Tab order** (`SessionTabStrip`): dragging a header shows an insertion marker at the nearest tab edge in
  the row under the pointer. A drop moves the existing tab in the order the window saves.
- **Ctrl+Tab and Ctrl+Shift+Tab select the next tab and the one before** (`ProjectWindow`), wrapping around, ahead of
  the terminal that has the keyboard (Magnus, 2026-11-21).
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
- **Specified** on Avalonia's headless platform, each scenario in a sandbox of its own (`Cloyd.Specifications/TheSandbox`,
  since 2026-11-20): a hub of its own, the tooling's `hub sandbox start` in a fresh folder, whose sessions run the tooling
  owner's stand-in for Claude Code, and projects, IDEs, saved windows and windows of Cloyd of its own. The sandbox is
  stopped and deleted as its scenario ends, about 0.7 s a scenario. A class whose facts only look at what its scenario
  built builds it once for them all (`SpecificationOpeningWindows.Once`). They run in about 2.5 minutes in one run,
  and in about a minute in `Cloyd/run-the-specifications.ps1`'s runs at once (see `development-environment.md`). They
  touch no session of Magnus's, and draw each state, and each wait in vain, to `bin/.../RenderedWindows/`. One
  scenario runs the real `Cloyd.exe` in a job that ends everything in it as it closes, as an IDE's may: its
  application's window shows on the desktop for a few seconds.

**Next, in this order:**

1. **A notification's click shows the session in Cloyd:** Cloyd answers it, built 2026-11-20; the hub's side is the
   tooling owner's, published the same day (`~/.claude` 1046b04, 6091138). The hub asks `POST /sessions/show` first, then
   brings the window forward itself, finding it among Cloyd's windows by the title the 200 answers. The IDEs are no
   longer asked. Not yet seen with Magnus's Cloyd, which runs the build from before until he restarts it.
2. **Clickable file paths and C# symbols:** what links is found, and each window keeps its files and symbols current
   (below). Showing them in the terminal is dropped (Magnus, 2026-11-20: "So better build the new UI than spend a ton
   of efforts on the UI we don't use"): the terminal "is just a backup once we get the new UI".
3. **The chat, to the desktop app** (below), with the links in it. Started 2026-11-20: see "The chat, as it's built".

## Clickable file paths and C# symbols: the plan

**In the chat, not the terminal** (Magnus, 2026-11-20): the terminal "is just a backup once we get the new UI. So
better build the new UI than spend a ton of efforts on the UI we don't use." The chat renders Claude's output in Cloyd's
own controls, which can show links as they like, where the terminal, Iciclecreek's, has no hook for them. So:

- **Kept, and used by the chat as they are:** the link finder, each window's files and symbols, and `Cloyd.exe
  --links-in`, all built.
- **Built with the chat, when its first link is clicked:** the click below.
- **Dropped:** links in the terminal, and with them the fork of Iciclecreek for a link-provider hook, which Magnus had
  chosen over a layer of Cloyd's own before this. The terminal keeps what Iciclecreek shows: URLs and OSC 8 links,
  which a Ctrl+click opens in the browser.

Claude's calls, 2026-11-20, unless marked as Magnus's:

- **What links is what links in Rider's terminal** (`architecture.md`), the symbol linker's rules ported to .NET in
  `Cloyd.Links`, its unit tests ported with them:
  - a path links when every segment lines up with the end of a project file's path, with `:line`, `:start-end`,
    `:line:col` or `#L…` after it;
  - a word links when it names a C# type or member of the project's solutions, and a dotted `Type.Member` as one link
    when that type has that member;
  - the Kotlin finder stays for Rider's terminal: the same rules in two languages.
- **Built: the files and symbols of each project window** (`ProjectWindow.FilesAndSymbols`), read from the first time
  something asks what links, which nothing does until the chat links: before then, reading them would cost git's and
  Rider's time for nothing.
  - **The project's files:** `git ls-files --cached --others --exclude-standard` in the project's folder, which lists
    the tracked files and the new ones git doesn't ignore: 48 ms for this repository, 62 ms for Compze's 2,625 files.
    Read every 5 s, so a file Claude Code just wrote links when it names it. A project that isn't a git repository has
    no file links.
  - **The C# symbols:** from Royder (`DotNetAnalysisAndRefactoringMcp`), as the plugin gets them: `list_solutions` for
    the solutions in the project's folder or a folder in it, then `browse_namespace`. 358 ms for Cloyd's solution,
    3.5 s for Compze's. Gathered as the window opens and every 2 minutes, so that the symbols Claude Code adds link
    soon; every 10 s while Rider doesn't answer, as while it loads the solution as the IDE opens. No cache on disk: after
    each start of Cloyd, symbols link once the first gathering ends.
  - **Specified** with a stand-in Royder (`StandInDotNetAnalysisAndRefactoringMcp`) and a git repository of the
    specification's own; and against Magnus's Rider with `Cloyd.exe --links-in` (`development-environment.md`).
- **A click:**
  - a file or a symbol opens in the IDE that shows the window's project, through a new `POST /files/open` of Claude
    Sessions' local API, in an editor of that project, the IDE's focus on it;
  - Cloyd then brings the IDE's window forward itself, as the hub brings Cloyd's: in the foreground at the click, it may.
    Not the IDE: it couldn't without Cloyd letting it, and the specifications, which ask the sandbox Rider to open files,
    would have it take Magnus's focus whenever he had left the machine alone for Windows' foreground lock timeout;
  - several declarations or files show a picker in Cloyd; none, the window's foot says so;
  - a URL opens in the browser.
- **Specified:** the finder by the ported unit tests; the rest on the headless platform, with a stand-in Royder and the
  `StandInIde`; the IDE's side in the sandbox Rider.

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

### The chat, as it's built

**First increment, 2026-11-20: the conversation once a turn is done, read only** (`Cloyd.Chat`).

- **A switch at the top of each tab, `Chat` and `Terminal`:** the terminal at first, until the chat can do what Magnus
  needs, as sending; then the chat, the terminal staying the backup. The chat first since 2026-11-21: see "While Claude
  works, as it's built".
- **Where the conversation comes from:** the hub's `GET /sessions/{sessionId}/conversation/stream`
  (`TheHub.ConversationAsWritten`), for the session that runs in the tab or ran in it last, along its line, so across a
  `/clear`. Followed only while the chat is shown, as the hub holds a conversation in memory while a stream follows it.
- **What it shows** (`ShownInTheChat.AllOf`), as `cloyd-chat-specification.md` has the app show it: Magnus's prompts in
  bubbles; Claude's markdown, with LiveMarkdown.Avalonia, the spike's; tool calls one after another folded into a grey
  line that counts them, `Read 2 files, ran 2 commands, 1 failed`, with `+9` for the lines edits added, opening to a
  row for each, a call by itself as its row; a command's row opening to the command behind `$ ` and its output; a
  `/compact` before its compaction's line, `Compacted session · saved 81.5K tokens`. Not shown, as in the app: Claude's
  thinking, interruptions, a background task's notice, Claude Code's own tools for its task list.
- **Drawn again only where it changed:** each time the hub's stream has caught up, the chat compares what it is to show
  with what it shows, from the top, and redraws from the first difference: what is added, and nothing after a publish
  of the hub, which sends the whole conversation again.
- **The end of the conversation, as a plain list** (`DrawnEndOfTheConversation`): the last 100 things shown, and 100
  more each time Magnus scrolls up near the top of what is drawn, what he looked at staying where it was. Not a
  virtualizing list: it guesses the length of what it hasn't drawn, and in Magnus's own long sessions its guesses moved
  the end without end, the chat flickering and Cloyd not answering (2026-11-20), as a chat's heights range from a line to
  a screenful and change as Claude's markdown is laid out. Specified with the sample session a hundred times over
  (`TheSampleSession.RepeatedInTheSandbox`).
- **Following the end** (`WhatTheViewFollows`), or a match a search shows in place of it, is told by what Magnus does,
  the wheel, the keys and the scroll bar, not by where the view is: what is drawn grows without him.
  Opening a transcript row pauses following so the heading stays in place. While the scrollbar thumb is held,
  the mouse controls the viewport. Releasing it at the bottom resumes following.
- **Windows' theme, as the app follows it** (Magnus, 2026-11-20): Cloyd follows it, light or dark, and the chat's
  colours (`ColoursOfTheChat`) are near the app's in each, its code blocks in Visual Studio Code's colours for each. The
  terminal stays dark in either, as Iciclecreek's terminal draws itself.
- **Specified** on the headless platform with the sandbox hub: the session the app's pictures show
  (`TheSampleSession`), drawn to `RenderedWindows/the-sample-session.png` to set beside them; a run of calls and a
  command opened; and a tab switched to the chat of a live session of the stand-in for Claude Code.
- **Cloyd's own, where the app wasn't seen:** a prompt from another session, a wake-up or a program as a grey line with
  its first line; a clearing as `Cleared · a new session begins`; an edit's row opening in place to its change line by
  line, where the app opens a pane at the side.
- **Not yet as the app:** inline code has no grey ground, and a code block has LiveMarkdown's bar with its language,
  which the app's hasn't.

**Before more of the chat: the session history picker** (below), reopening this session and old ones. Magnus,
2026-11-20: "Not having a simple clear way of getting this session and old sessions opened again is a bigger problem
than any polish that the chat UI might need."

**Then the chat's next increments:** the input box, sending through the hub, and then the chat as each tab's first view; while Claude
works (the line at the end, the text as it streams, the task list); scrolling's `Scroll to bottom` button; the buttons
and time under a turn; links in Claude's text, with `POST /files/open`; a permission prompt switching to the terminal.
Built by 2026-11-21: the input box, the chat first, the working line, the text as it streams, the box while Claude
waits in the terminal. Still to come: the task list, `Scroll to bottom`, the buttons and time under a turn, links.

**The input box, Magnus's calls (2026-11-21),** where the app's spec doesn't reach, as it covers only the conversation:

- **Ctrl+Enter sends, Enter starts a new line,** as the Kotlin chat had it: "ctrl+enter for now".
- **Nothing interrupts Claude by accident.** Escape doesn't: "This trips me up again and again. I virtually never
  intended to interrupt claude when this happens. IMO it is a disasterpiece of UI design." Nor does sending: a message
  sent while Claude works waits, and Claude Code takes it when it can, as its own box does ("I do not want sending a
  message to kill the ongoing work either. Absolutely not."). Interrupting is a Stop button of its own, shown while
  Claude works, apart from the send button.
- **A question only the terminal can answer,** a permission prompt or a question with choices, shows as a box between
  the conversation and the input box, prominent, saying Claude waits for him in the terminal, with a button that shows
  it (Ctrl+H). The tab doesn't switch by itself, as his keys could then answer the prompt. "Make it much more prominent
  than that. I could miss that for hours."
- **Restarting Cloyd as each part is done** is fine, unasked: "OK to restart when done with parts, no test needed. It
  should work." What he types in a terminal is in Claude Code's box, which redraws it as the window opens again.
- **The terminal's own keys stay as they are** for now ("Leave the terminal shortcut handling alone for now"): Escape
  goes on to Claude Code there.

**The input box, as it's built (2026-11-21,** `Cloyd.Chat.Writing`), the Kotlin chat's (`claude-sessions.md`, "The
input box") ported, with Magnus's calls above:

- **`InputBoxOfTheChat`,** under the conversation, as wide as its column: AvaloniaEdit's `TextEditor`, as the spike had
  it, in a rounded border, with a round send button in Claude's orange and the **Stop** button beside it while the
  Claude Code's status (`StatusOfAClaudeCode`, from the hub's running sessions) is `busy`. It grows with its lines up to
  two fifths of the chat, then scrolls.
- **`DraftOfAMessage`:** the editor's document, with each stretch Magnus pasted marked as a `PastedStretch`, text or an
  image's placeholder, in AvaloniaEdit's `TextSegmentCollection`, which moves the stretches as he edits around them and
  grows one he types inside. Its `Parts` walk the text, the innermost stretch deciding; an image's placeholder he edited
  is text. Line breaks go to the hub as line feeds: the editor breaks a line with CR LF.
- **Pasting:** Ctrl+V and Shift+Insert, taken before the editor's own paste: the clipboard's text as pasted, else its
  image, saved by `PastedImages` as the terminal's are, as `[Image #n]`, with thumbnails over the box (Windows' viewer
  shows one whole on a click).
- **`SendingFromTheChat`** gives the messages to the hub's `POST /running-sessions/{processId}/send` one after another,
  to the Claude Code that ran in the tab as each was sent. One the hub refused comes back into the box with those that
  waited after it, ahead of what Magnus wrote since, and the hub's `error` shows over the box in red.
- **Each message is a `MessageOnItsWay` until the conversation has it** (Magnus, 2026-11-21: "it should not appear to
  disappear until the agent picks it up"): shown over the box, dimmed, in a prompt's bubble cut at three lines, with
  `Sending…` under it, or `Queued` once the hub's answer says Claude Code queued it, as Claude Code's own UI says
  under a queued message. How the desktop app shows one wasn't seen. It goes once one more prompt of the conversation
  holds its text than as it was sent: held in, not equal to, as Claude Code may give the model several queued messages as
  one prompt. One given to a Claude Code that no longer runs in the tab goes too. Not covered: a queued message Magnus
  takes back into Claude Code's own box with Up and changes there stays shown until that Claude Code exits. Shown by
  `MessagesOnTheirWayOfTheChat`, a row of the chat's own between the call under way and the working line (Magnus,
  2026-11-21: "place queued messages above the turn information with the spinning icon, but below what the session is
  doing currently which is the reason the message is queued"); the box tells of changes with
  `InputBoxOfTheChat.MessagesOnTheirWayChanged`.
- **Saved with the window** (`SavedSessionTab.Draft`): the messages that waited to be given, then the draft, each part
  as it came in; they come back into the box, to be sent again by Magnus. Not the message being given, which the hub
  goes on typing whatever becomes of Cloyd.
- **The status line under the box** (`Cloyd.Chat.Status.StatusLineOfTheChat`): Claude Code's own, all of it, as the
  terminal shows it under Claude Code's box (Magnus, 2026-11-21: "get all the information from our custom status line
  in the CLI into the status line here"): the running session's `statusLine.text` (`ClaudeCodesStatusLine.Text`), the
  machine's status line command's, in its colours: the hub's `styledStretches` (`StretchOfTheStatusLine`, asked of the
  tooling owner and published by it the same day, 4312305), each a run in its style, the terminal's eight colour names
  shown in Cloyd's own shades (`ColoursOfTheTerminal`), readable in Windows' light theme and its dark, the default in the
  line's grey; and under it the permission mode,
  `⏵⏵` and its `shownAs`; under that, what runs in the background (see "What a session runs in the background" below).
  "No Claude Code runs in this tab" while none runs. What Claude does isn't told there: first a
  left side said it, and Magnus, of its "Idle": "This is not needed with progress clearly shown above the input." The
  tab tells the chat its running session (`ChatOfAClaudeCodeSession.ClaudeCode`), which hands it to the box and the lines.
  Specified with running sessions made in the specification, as the stand-in shows the hub no status line yet.
- **Specified** with the stand-in through the sandbox's hub (`Cloyd.Specifications/Chat/Writing`): a message of two
  lines sent; text and an image pasted, kept apart, and sent; Escape and a message sent while Claude works, which
  interrupt nothing, and the message shown over the box as sent, as queued, and no more once the conversation has it;
  Stop; a message refused while Claude Code's own box holds text; a draft through the window's closing; a slash command
  showing the terminal.

### What a session runs in the background, as it's built

Magnus, 2026-11-21: "I have no way in the new UI to see that a session in waiting on something"; and "add the missing
information about background work under the existing two lines under the input box". The TUI shows `· 1 shell · ↓ to
manage` after the permission mode, and after a turn `✻ Sautéed for 21s · done 2:37 PM · 1 shell still running`.

- **From the hub:** each running session's `backgroundWork` (`Cloyd.Hub.RunningSessions.BackgroundWork`, on `RunningClaudeCode`), asked of
  the tooling owner and published by it the same day: the tasks (`BackgroundTask`: kind, Claude's description, a
  shell's command, a subagent's type) and the scheduled prompts (`ScheduledPrompt`), as Claude Code told the hub's
  `Stop` hook at the end of the session's last turn. So a task that ends while Claude works stays listed until that
  turn ends. Null until a turn has ended since the hub started, as after a publish of it: then only the status `shell`
  (`StatusOfAClaudeCode.IsIdleWithBackgroundWork`) tells that some runs, and the chat says "A background shell or
  monitor runs" (`BackgroundWork.SomeRuns`). `RunningSession.BackgroundWorkCounted` says which of the two.
- **Under the box,** the status line's third line: it counted by kind, as the TUI counts it, such as "1 shell" or
  "2 shells · 1 monitor · 1 scheduled prompt", whether Claude works or not; its tip lists each with what it does
  (`BackgroundWork.Listed`), in place of the TUI's list that `↓ to manage` opens.
- **In the tab's header,** `MarkerOfBackgroundWork`: Claude Code's ✻, still and grey, in the place the spinner turns in
  while Claude works, as high and wide, while Claude doesn't work and background work runs; its tip says what runs.
- **After each turn, a semibold timing line and a thin divider** (`ShownInTheChat.EndOfATurn`, drawn with the same
  `TurnEndLine` as Codex Chat): "Worked for 21s · done 14:37", from the hub's
  `endOfATurn` messages, which the tooling owner published on 2026-11-21 from the `turn_duration` entries Claude Code
  writes in the transcript at each turn's end. An interrupted turn has none, in the transcript and so in the chat. As
  Claude Code's TUI writes it, read from its bundle (2026-11-21): `${verb} for ${duration} · done ${doneAt}`, then
  `· ${count} still running`, its count of shells and monitors joined by commas, "1 shell, 1 monitor".
  - **"Worked",** where the TUI picks a verb of its whimsy, such as "Sautéed".
  - **The time** (`Cloyd.Chat.Status.WhenItEnded`) as the TUI's: alone for a turn that ended today, with the weekday in
    the last week, else with the date, by calendar days; in the machine's culture, where the TUI uses its own (Magnus's
    TUI says "2:37 PM", his Windows "14:37").
  - **"· 1 shell still running"** only after the conversation's last turn, while that turn's end is the last thing
    shown, from `BackgroundWork.TasksCounted`: the hub tells the background work as of the last turn's end only. Scheduled
    prompts aren't counted: they don't run until they wake the session.
  - A turn's end also ends the turn for the working line's count of commands (`ToolCallsOfTheLastTurn`): a turn woken by
    a prompt the chat doesn't show, as a background task's notice that it ended, no longer counts the commands of the
    turn before.
- **Specified** with the stand-in's `Run a background shell for 8 seconds` (`When_Claude_runs_a_shell_command_in_the_background`):
  "1 shell" under the box, its tip with the shell's description and command, the tab's marker, and "· 1 shell still
  running" after the turn; then, once Claude replies to its end, all gone. The kinds the stand-in doesn't run with
  background work made in the specification (`When_a_chats_status_line_shows_what_runs_in_the_background`). The turn's
  end line after the stand-in's turn of 10 seconds (`When_the_hub_tells_of_Claudes_turn`), and how its time is said
  (`When_the_line_after_a_turn_says_when_it_ended`).

### Searching the conversation, as it's built

Magnus, 2026-11-21: "How hard is a search in the transcript to write? If easy, please do it right away using standard
search keyboard shortcuts. Ctrl+F F3 etc". As in a browser (`Cloyd.Chat.Searching`):

- **The keys,** wherever the keyboard is in the chat: Ctrl+F opens the find bar (`FindBarOfTheChat`) at the top right of
  the conversation, its box selected to be typed over; F3 the next match, Shift+F3 the one before, and in the box Enter
  and Shift+Enter; Escape closes it, the keyboard going back to the input box. The terminal keeps its keys: there, Ctrl+F
  goes to Claude Code, as other keys do.
- **Down, as a browser:** the matches are numbered down the conversation, and the next match is the one below (Magnus,
  2026-11-21: "I want it to go down please", of the first build's going up, as Windows Terminal searches by default).
  A search starts at the first match from the top of the view on, as a browser's does; with none below, at the last
  above it, rather than one back at the conversation's start. Searched for as typed, case not mattering.
- **The whole conversation, drawn or not** (`FoundInTheChat`): each thing shown says what text it shows
  (`ShownInTheChat.TextShown`), Claude's markdown as markdown, a run of tool calls its line and not the rows it opens to.
  A match above what is drawn is drawn up to (`DrawnEndOfTheConversation.DrawAsFarUpAs`).
- **Painted by LiveMarkdown** (`MatchesPainted`): its `MarkdownTextBlock` paints named highlight ranges, in styles the
  chat sets for Windows' theme, and its renderer searches Claude's markdown as rendered, painting again as it grows. The
  bubbles, grey lines and lines that open are drawn with `MarkdownTextBlock` too, to be painted alike. Each match drawn
  yellow, the one shown orange. Where the markdown rendered has fewer matches than its source, the last is shown.
- **The view follows the match shown** (`WhatTheViewFollows.FollowInPlaceOfTheEnd`) until Magnus scrolls: drawn far up,
  the things above it grow as their markdown is rendered after the scroll, and the view's anchor moved it away (seen in
  the specification, 2026-11-21). It sets the scroll viewer's offset itself, the match in the middle of the view unless
  in view already: the first build had the match's text block ask to be brought into view, and from in Claude's
  markdown the request never reached the chat's scroll viewer, so a match there out of view was never scrolled to
  (Magnus, the same day: "hits ... are invisible and no scroll happens"; 5 of 11 in one of his conversations).
- **Not in what is folded:** a run's rows, a call's command and output aren't searched, so no match is out of sight.
- **Specified** with the sample session 8 times over, 16 matches of "started." in Magnus's prompts and Claude's replies
  (`When_Magnus_searches_a_chat`): the count, the first match in view shown, every match drawn painted; F3 twice, round
  to the match furthest up, drawn for it; Shift+F3 up to a match in Claude's markdown out of view, scrolled to; Escape.

### While Claude works, as it's built

**2026-11-21** (`Cloyd.Chat.Status`, `Cloyd.Hub`), the Kotlin chat's (`claude-sessions.md`, "A working line" and "What
Claude writes shows as it comes") ported, with Magnus's calls of the day.

- **The rows over the box,** shown while the Claude Code is `busy`, as Claude Code's own UI shows the call under way
  apart from its spinner line (Magnus, 2026-11-21: "put it above the input, like other claude UIs do and include the
  time and tokens like they do", then, of the two in one line, "I would like it separated like in the CLI"), the
  messages queued behind the call between them:
  - **The tool call under way** (`CallUnderWayOfTheChat`), the last of the turn's with no result (`ToolCallsOfTheLastTurn`, from what the chat
    shows of the conversation) ("so that I can see what the agent is doing right now"): what it does, in bold,
    `ToolCallInTheChat.WhatItDoes`, the description Claude gave a command, else the row's verb and target, and how long
    it has run since Claude made it (`MadeAt`), `Finding the repositories that use it · 5s`; under it, `WhatItWasGiven`,
    a command's first three lines but the empty ones, the first after `└ $`, an ellipsis after the third when more
    follow, as the TUI cuts it; or a file's path. Shown while the activity is `runningTools`, and always where the hub
    tells of no turn. It shows once the conversation has the call, which took a few seconds in a live check.
  - **The working line, the turn's, under them** (`WorkingLineOfTheChat`): a spinner of Claude Code's glyphs in Claude's orange (`SpinnerOfClaudeCode`, as
    high whichever glyph it shows: the glyphs' fonts differ in height, and the line jumped 2 pixels with each turn on
    Magnus's window; turning itself every 480 ms, a quarter of Claude Code's speed, as he asked twice; the same spinner
    shows before the title of each tab whose Claude Code is busy, "so that I can see which tabs have a working session
    in them"); what the turn does, in orange, from the running
    session's `turnUnderWay` (`TurnUnderWay`, `ActivityOfATurn`): "Waiting for the model…", "Thinking…", "Writing…",
    "Writing the Write call…", "Running tools…"; then, grey, in parentheses as the TUI has them, how long the turn has
    taken, its output tokens and the commands it ran: `Thinking… (1m 12s · ↓ 1.2k tokens · 3 commands)`.
- **Where the hub tells of no turn,** as in a session that doesn't load the `tooling-commands` mod: `Working…`, the
  time from when the line saw Claude Code turn busy, and the call under way over it.
- **The box while Claude waits in the terminal** (`WaitingForMagnusInTheTerminal`), between the conversation and the
  working line, while the status is `waiting`: Claude's orange, white text, "⚠ Claude waits for you in the terminal",
  what for, from `waitingFor`, such as "Permission prompt: answer it in the terminal.", and a white **Show the
  terminal (Ctrl+H)** button, which switches the tab. The tab doesn't switch by itself, as Magnus asked.
- **What Claude writes, as it comes** (`TheHub.BlockBeingWrittenIn`, `TurnAsStreamed`, `BlockBeingWritten`): the hub's
  `GET /running-sessions/{processId}/turn/stream`, followed while the chat is shown, for the Claude Code that runs in
  the tab; the block of text, not thinking, which the chat doesn't show, drawn after the conversation as Claude's text,
  until the conversation has it: an ended block stays until the conversation changes next, as in the Kotlin chat. A
  block the turn's end cut short goes. Each piece draws the block's markdown anew, four times a second at most.
- **`TheHub.EachStreamAt`** follows any of the hub's streams from each hub in turn: the running sessions', a
  conversation's and a turn's.
- **Tabs open on the chat** (`SessionTab`), now that a question only the terminal can answer shows prominently. The
  terminal stays laid out under the chat, transparent and out of the mouse's reach, rather than hidden: it attaches to
  its host only once laid out, and reads what the session draws at the tab's size only while it is.
- **Specified** through the sandbox's hub, which the stand-in tells of its turns as the mod tells it of Claude Code's
  (since 2026-11-21): a turn of 10 seconds that waits for the model, thinks, writes a PowerShell call, runs it and
  writes its reply, which ends, or which Magnus stops while Claude writes (`When_the_hub_tells_of_Claudes_turn`).
  `TheTurnAsTheChatShowedIt` looks at the chat every 10 ms through the turn, so that the facts tell what it showed at
  each step, not only at the moment they look. The stand-in finds the hub through `CLAUDE_TOOLING_LOCAL_API_FOLDER`,
  which the hub gives its sessions' shells, as it gives `CLAUDE_CONFIG_DIR` (the tooling owner's 6cc7fa2). With made data:
  the `Working…` of a session whose turns the hub isn't told of, and a command under way
  (`RenderedWindows/a-chats-working-line.png`), and the box (`a-chats-box-while-claude-waits-in-the-terminal.png`).

### The session history picker, as it's built

**Built 2026-11-20** (`Cloyd.History`): the Kotlin plugin's picker (`claude-sessions.md`, "The session history
picker"), ported, laid over the project's window.

- **Opened by the History button beside the +, and Ctrl+Alt+Shift+Y,** which the window takes before the terminal
  with the focus would pass it to Claude Code. Escape, or a click beside the picker, closes it. Since 2026-11-21 both
  buttons are at the start of the tab strip, as high as the tabs, as icons of Windows' icon font (Segoe Fluent Icons'
  Add and History), on Magnus's word; the strip's look, as tabs of what they show, is `Cloyd.Theming.LookOfTheTabs`,
  through the Fluent theme's own tab resources.
- **What it lists:** the hub's `GET /sessions`, every session Claude Code has a transcript of, most recently used
  first, but those with no conversation to resume and those `claude -p` ran. Each with its title, else its last prompt;
  below it, greyed, its last prompt, when it was last used, and its folder and branch in the wider scopes. Typing
  filters by every word, in any case. Up and Down move the selection from the search box, Enter or a double click
  chooses.
- **Its scopes,** as in the plugin: the git worktree the project is in, every worktree of its repository, all projects
  (`ProjectWorktrees`, through `git rev-parse` and `git worktree list`). A session the hub knows no directory of is
  covered only by all projects: the hub takes the directory from entries that name a git branch, and the stand-in for
  Claude Code writes none (told to the tooling owner, 2026-11-20).
- **Where each session is decides what choosing it does** (`WhereTheSessionIs`), from the hub's running sessions and
  hosts, and the tabs of every one of Cloyd's windows (`ProjectWindows`, which each window joins as it opens):
  - running in a tab, of this window or another project's: the tab is selected and its window brought forward;
  - exited in a tab, its Claude Code at the shell's prompt: resumed in that tab;
  - running in a session host no tab shows, as after its window closed, or a console attached to it: shown in a new tab
    of this window, which takes it over from the console. Cloyd's own call: the plugin had no session hosts when its
    picker was built;
  - running in no session host, as in a terminal of its own: greyed out, and it can't be chosen;
  - otherwise: resumed in a new tab, in the project's folder.
- **Specified** with transcripts the specifications write (`SessionOfASpecification`), in a project with a second
  worktree, and with live sessions of the stand-in in each of those places, one of them in a terminal of its own
  (`ClaudeCodeInATerminalOfItsOwn`): `RenderedWindows/the-session-history-picker*.png`.

### Codex sessions in the terminal tabs: the plan

**`cloyd-gpt`'s, 2026-11-21; built the same day.** Magnus: "I want gpt sessions in the cloyd UI next is the thing", then
"Terminal tabs first. Chat can wait :)", and "getting this in is highest priority". The hub serves Codex sessions with its
protocol 2: the agreed proposal is `~/.claude/dev_docs/the-local-api-protocol-2-proposal.md`, the contract
`~/.claude/dev_docs/the-hubs-local-api.md` ("Protocol 2, for Codex sessions"), its types in
`ClaudeTooling.Hub.LocalApi.Contract` (home-claude 23d88f8). Codex itself, and the stand-in for Codex the specifications
run, are `gpt-collaborators`'s (`~/.claude/dev_docs/codex-sessions-through-the-hub.md`).

**What's generic and what isn't,** on Magnus's word: listing sessions is generic ("It is just different types of
sessions"); what happened in a session, the chat and its classes, stays each agent CLI's ("making all of the
chat-UI/transcript-data-shape etc, generic [...] would probably cause more problems than it solves"). "Session" is the
word for every agent CLI's; "thread" only in code that speaks Codex's app-server protocol, which Cloyd doesn't.

**Built:**

- **Sessions of any agent CLI:** `Cloyd.Sessions.Session` and `SessionId`, Claude Code's `ClaudeCodeSession` and
  `ClaudeCodeSessionId`, Codex's `Cloyd.Codex.CodexSession` and `CodexSessionId`. Tabs, saved tabs, resuming and the
  history's choices take a `Session`; a saved session is saved with its `agentCli` (`claudeCode`, `codex`), and one
  saved without reads as Claude Code's (6b5d7ba, 4300aa6).
- **The commands:** `Cloyd.Hub.Commands.AgentCliCommand`: `ClaudeCodeCommand`, and `CodexCommand`, `codex` or
  `codex resume <id>`, typed through `TheHub.StartCodexIn` (`POST /shells/{processId}/start-codex`). A name given goes
  in the request; the hub names the session once Codex shows its id (4300aa6).
- **The running sessions:** `RunningSession`, what a tab needs of any agent CLI's (its session, host, whether it's
  busy); `RunningClaudeCode`, with what only Claude Code tells, which the chat takes; `RunningCodex` with
  `StatusOfACodex`. The hub's listings read both, leaving out an agent CLI Cloyd doesn't know (af8601e, agreed with
  `.NET-cloyd`).
- **The agent CLIs, as a tab knows them:** `Cloyd.Sessions.AgentCli`, Claude Code or Codex: the title of a session
  with no name ("Claude", "Codex"), whether it has a chat, the command of a new session. `NewSession(AgentCli, Name)` is
  what the + asks for. `SessionTab.AgentCli` follows the session that runs in its host, or ran there last, or the new
  session it was opened for; a Codex session's tab shows its terminal only, with no switch to a chat (be5778c).
- **The + button:** offers the agent CLIs the hub serves (`AnnouncementOfTheHub.AgentClisItServes`: Codex while the
  announcement's `codexFolder` isn't null), with a choice above the name box while it offers more than one, Claude
  Code chosen at each opening (b5a1908).
- **The history picker:** one list, most recently used first, both kinds interleaved, each entry marked with its kind;
  toggles for Claude Code and Codex, both on at first, so that each combination can be shown (Magnus: "clearly
  delimited and filter options on whether to show each combination", and "One list: most recently used first, both
  kinds interleaved, each entry marked with its kind"). `Cloyd.History.SessionHistory` holds `SessionOnDisk`s of every
  agent CLI the hub serves, the hub's `GET /sessions` read by `SessionOnDisk.EachIn`, and the agent CLIs the hub serves,
  which the picker has a toggle for (`SessionHistoryPicker.AgentClis`, `ListTheSessionsOf`, `LeaveOutTheSessionsOf`).
  An entry is called by its session's name, else its prompt: a Claude Code session's last, a Codex session's first
  (`firstPrompt`; Codex's listing has no last prompt). Marks and toggles show only while the hub serves more than one
  agent CLI (`TellsTheAgentClisApart`).
- **Bringing back a saved Codex tab** whose host is gone: resumed while the hub lists the session on disk, as a Claude
  Code one is while its transcript is there (`Cloyd.Projects.ResumableSessions`, which asks the hub once for each agent
  CLI, as the first tab of it needs it).
- **Specified** against the sandbox hub, which serves Codex sessions through the stand-in for Codex
  (`~/.claude/src/StandInForCodex`, first on the sessions' shell `PATH`): a new Codex session in a tab, named as Magnus
  named it, its pulsing dot while the stand-in is busy; the picker's marks and toggles over sessions of both, and resuming a
  Codex session from it; saved Codex tabs brought back after their hosts are gone; the + offering both. A Codex session
  on disk is written as the stand-in keeps it (`CodexSessionOfASpecification`, the stand-in's `RecordOfAThread`, a
  format gpt-collaborators keep as a contract with these specifications).
- **Protocol 2 was published on 2026-11-21 at 18:35.** Cloyd compiles the contract's source that each publish puts
  beside the published hub, `~/.claude/bin/ClaudeTooling.Hub.LocalApi.Contract`, so it speaks what the hub that runs
  speaks. The environment variable `TheHubsContract` names another source, such as `~/.claude/src`'s, to build against a
  change before its publish. A protocol bump still needs a Cloyd deploy, and `everyday reload Cloyd` on Magnus's go:
  until then his running Cloyd refuses the new hub.

A named new Codex session's tab is titled "Codex" from the hub's first listing of it until the hub has named it,
a second or two later.

### Chat in a Codex terminal tab

The hub advertises `supportsCodexChat` in its protocol 2 announcement. Cloyd offers Chat and Terminal for the same
Codex session when that capability is present. Older hubs can still open Codex terminal tabs.

- `SessionChat` owns the conversation column, Markdown, scrolling and search. `ChatComposer` owns the editor, paste
  handling and keyboard shortcuts. `ChatOfAClaudeCodeSession` and `ChatOfACodexSession` each interpret their own
  session's conversation and decide how to submit input.
- Opening Codex Chat follows `GET /codex-chat/{sessionId}/stream`. Its first event supplies a snapshot. Each
  subsequent turn replaces the turn with that ID. Item text is cumulative. After a reconnect the new snapshot
  supplies everything that happened while Cloyd was disconnected. An exited session's tab reads
  `GET /codex-chat/{sessionId}` once. Reading the saved snapshot leaves the session unloaded.
- Ctrl+Enter submits input to the displayed session. While a turn runs it steers that turn. Stop names the same
  observed turn. Terminal owns approvals, including one that was pending when Chat opened. The hub can explicitly
  refuse a stale command. If an acknowledgement is lost, Cloyd puts the message back in the editor for Magnus.
  A steer has its own client message ID. Cloyd retains its parts after acknowledgement until that ID appears in
  the conversation. When the turn ends without it, the steer returns ahead of newer typing. Pending steers are
  saved with their own session's draft when the window closes. Resending is always explicit.
  A missing turn or unknown status leaves the steer pending; Chat offers to copy that input into the editor.
  If a transport error returned an unedited copy and later history confirms delivery, Chat withdraws only that
  copy. Work already in the editor and later edits remain. With an older hub, Chat keeps the draft and directs
  Magnus to Terminal for steering during the upgrade.
- Codex 0.171.0 can omit the beginning of an in-progress reply on the first attachment. Its completed item
  supplies the full text. Also, if Terminal starts a turn just as Chat submits an idle Send, Codex can steer that
  new turn with the submitted text. The tooling's `dev_docs/the-hubs-local-api.md` records both behaviors.

Both chats show Magnus's prompts in gray bubbles on the right and the session's Markdown replies in blue
bubbles on the left. Peer messages in either direction start folded, with just the peer name and protocol.
Received messages are violet. Sent messages are teal with an outgoing arrow. Both folded bubbles are
borderless and one message line high. Opening a peer message reveals its body and any recorded delivery details.
Cloyd resolves session IDs using the hub's running session names and looks up saved names for exited peers.
If neither source has a name, the recorded address identifies the peer.
Codex records a Peers delivery as a text prompt wrapped in `<peer-message>`. Claude Code records its own
peer sender, or receives Peers through `<channel source="plugin:peers:peers" …>`. Cloyd reads only the opening
tag for attribution and displays the body as plain text. Claude prompts recorded as human keep their authorship.
A verbatim copy of the complete Codex envelope has the same representation in Codex history as a Peers delivery.
The Claude Code `SendMessage` tool, Peers' `send_to_peer` and Codex's native `send_message_to_thread`
each appear once as outgoing messages. Codex's native incoming delegations are identified by the tool that
delivered them and its envelope. Other tools stay in the expandable tool-call rows.

In Codex Chat the active tool and the turn's progress sit above the composer. The blue `CodexProgressDot` there
and in the tab header grows and shrinks during a turn. Between turns, the header keeps a still dot for Codex
and a warm star for Claude Code. Each indicator occupies the same space through a change of activity.
The turn and item clocks come from
Codex, so opening Chat halfway through a command preserves its elapsed time. A completed turn uses the same
duration and local time formats as Claude Code Chat. An older history can lack those measurements.

The hub supplies the Codex footer in the first live snapshot and replaces it with `footer` events as session
settings, token usage and account limits arrive. Cloyd renders the supported configured items below the composer
in their native order, with wrapping and theme-aware colours. Chat omits the session ID that Peers reads in Terminal
and the session name already shown by the tab. The configured weekly limit shows the current account's
percentage used, even in a thread with no turns. It shows `week ?` until a reading is available.
Other missing readings leave their items out. The
background-terminal line uses the live session's `/background-terminals` endpoint on opening, at turn boundaries
and every five seconds while a turn or background terminal runs. The tooltip lists the commands. Saved
conversations show their recorded turns and do not query live terminals or account limits.

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
