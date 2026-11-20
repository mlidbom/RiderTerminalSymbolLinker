# Cloyd as a .NET application, in Avalonia, beside the IDEs

## Status

**Magnus's direction, 2026-11-20. Nothing is built.** Until it is, Claude Sessions stays the Kotlin plugin that
`claude-sessions.md` describes, and its chat stays the one Magnus works in every day.

- **Agreed (Magnus, 2026-11-20):** the direction below; the calls under "How it's to be built"; the input box as plain
  as Avalonia's editor gives it; one home, later, for the Avalonia extensions the apps share.
- **Open, put to Magnus on 2026-11-20:**
  - whether to run the spike below first;
  - how Cloyd's windows relate to the IDEs' windows (below, "What's still open").

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
- **The Kotlin chat stays Magnus's daily driver** until the .NET one does the everyday back and forth better. New chat
  features go into the .NET one once its work starts.
- **The input box is Avalonia's editor, AvaloniaEdit, unpolished,** with standard Windows text editing. Magnus: "None
  of the claude UI's have a super competent input box, very few chats do. Any polishing at all of this can wait."
- **The helper that runs a deploy between an IDE's exit and its start** can move to the .NET side, which outlives the
  IDE (tessaging-in-tooling): simpler than a helper the plugin leaves behind.

## What's still open

- **The TUI needs a host.** Slash commands, permission prompts, pickers and dialogs are drawn only by Claude Code's
  TUI, and a tab switches to it whenever one opens. In the IDE, its terminal hosts the TUI. The candidates, none tried
  yet:
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
- **How Cloyd's windows relate to the IDEs' windows,** across Magnus's 3-8 IDEs on their virtual desktops: one per IDE
  project window, beside it and following it to its desktop; one per virtual desktop; or one for everything. Claude's lean:
  one per project window, as the tool window is today, with a list of every session on the machine in each. Cloyd can
  move its own windows with the documented API; Deskmancer, Magnus's window-placement app, could place them too.
- **Publishing:** a .NET application and a plugin. Magnus, 2026-11-19: "How, and even if, we publish this is a
  question for the future."
- **Where the hub's contract types live** so that both `~/.claude/src` and this repository can reference them: the
  tooling owner's question.

## The spike proposed

Throwaway, about a day, on a branch, to settle the TUI's host and see how fast the porting goes:

1. Each candidate for the TUI's host above, attached to a session host running the real Claude Code's fullscreen TUI:
   resizing, the mouse wheel, pasting, colours, the alternate screen.
2. An AvaloniaEdit input box that sends through the hub.
3. One session's conversation, from the hub, rendered as markdown.
4. One specification on Avalonia's headless platform.

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
