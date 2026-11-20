# Cloyd as a .NET application, in Avalonia, beside the IDEs

## Status

**Magnus's direction, 2026-11-20. The spike is run; nothing lasting is built.** Until it is, Claude Sessions stays the
Kotlin plugin that `claude-sessions.md` describes, and its chat stays the one Magnus works in every day.

- **Agreed (Magnus, 2026-11-20):**
  - the direction below, and the calls under "How it's to be built";
  - the input box as plain as Avalonia's editor gives it;
  - one home, later, for the Avalonia extensions the apps share;
  - a Cloyd window per project, "for now";
  - the terminals first, and then all the Kotlin UI goes (under "How it's to be built").
- **The spike, run on 2026-11-20 on his go,** found the TUI's host and nothing against the port: see "What the spike
  found".
- **Open, put to Magnus on 2026-11-20:** starting the .NET Cloyd itself.

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
  | `chat` | 3,100 | rewritten. `claude-sessions.md`, "The chat view", is in effect its specification: what each part does, and Magnus's words on why. |
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
  - **The chat comes back afterwards,** built in .NET.
- **The input box is Avalonia's editor, AvaloniaEdit, unpolished,** with standard Windows text editing. Magnus: "None
  of the claude UI's have a super competent input box, very few chats do. Any polishing at all of this can wait."
- **The helper that runs a deploy between an IDE's exit and its start** can move to the .NET side, which outlives the
  IDE (tessaging-in-tooling): simpler than a helper the plugin leaves behind.

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
- **Where the hub's contract types live** so that both `~/.claude/src` and this repository can reference them: the
  tooling owner's question.

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
