# The development environment

What the plugins are built, checked and deployed with: the symbol linker, and Claude Sessions
([how it works](claude-sessions.md)), with the Cloyd Claude Sessions runs ([Cloyd](cloyd-as-a-dotnet-application.md)).
The commands are in `dotnet run --project tools/RiderTerminalSymbolLinker.DevelopmentTools`, which lists them.

## Building

- **JDK 21, kept in the repository** under `.tooling/jdk` (gitignored): the machine's only other Java is Rider's JBR 25,
  which the build's toolchain won't use. `build.ps1` points `JAVA_HOME` at it for one Gradle run, and so do the
  development tools. Look there before concluding that no JDK is installed. Why 21 and not 25 is at `jvmToolchain(21)`
  in `build.gradle.kts`.
- **`localIdePath`** in the user's `~/.gradle/gradle.properties` makes local builds use the installed Rider. Without
  it, Gradle downloads `platformVersion`, which CI does.
- **IDEA, with this project open,** gives inspections, builds and lookups into Rider's classes through its MCP server
  (`idea` in `.mcp.json`).

## The sandbox Rider

A second Rider with folders of its own under `%LOCALAPPDATA%\RiderTerminalSymbolLinkerSandbox`, started through
`RIDER_PROPERTIES`, which runs the working tree's builds of both plugins on a copy of
`tests/RiderTerminalSymbolLinker.FixtureSolution`, the fixture solution. Its window goes on IDEA's virtual desktop, on
the other monitor. Claude Sessions runs in any JetBrains IDE, and is checked in this Rider too: what it uses is the
platform's and the terminal's, alike in every IDE.

- **Plugins of its own:** `sandbox install` builds with Gradle's `prepareSandbox`, which every project has, and copies
  our plugins into the sandbox's plugins folder. Loading it straight from Gradle's folder kept Gradle from building while the sandbox ran:
  its .NET backend holds the MCP plugin's DLL open.
- **The MCP Magnus runs, by default:** the .NET Analysis&Refactoring MCP comes in two parts, its plugin and its tools
  builds, which the plugin swaps to at its next call, without a restart (the MCP's `dev_docs/swappable-tools.md`).
  extend-mcp deploys both to the everyday Riders ahead of the Marketplace: the plugin to their plugins folder, the
  tools builds to `%LOCALAPPDATA%\DotNetAnalysisAndRefactoringMcp\tools`. `sandbox install` copies both, the tools
  builds into the sandbox's `mcp-tools`, which `DOTNET_ANALYSIS_AND_REFACTORING_MCP_TOOLS` names there. So the
  specifications check the plugin against what Magnus runs and what the MCP releases next. A running sandbox restarts
  when the MCP's plugin changes, and swaps to new tools builds at once. `sandbox install --mcp marketplace` runs the
  Marketplace's build instead, the one Gradle's `prepareSandbox` fetches: what the plugin's users run.
- **Reloading without a restart:** the sandbox runs with `idea.auto.reload.plugins`. Windows won't overwrite a jar a
  running Rider maps into memory, but lets it be moved, so the old jar is moved aside first and deleted by a later
  install. Rider notices changed plugin files only when it looks at its plugins folder, which nothing watches, so
  `install` asks it to look through the observation channel. It reloads a plugin only when one of its files' content
  changes, never when a file is created or deleted (`DynamicPluginVfsListener`, Rider 2026.3), so the plugin's jar is
  named without the version (`composedJar` in `build.gradle.kts`): a version bump would otherwise rename it, and take a
  restart. A reload takes about a second; the indexes come back, since the startup activities run again.
- **A reload reuses the plugin's descriptor as Rider first loaded it:** the plugin.xml, and the jar paths, it read at
  startup. So a changed plugin.xml, such as an extension added or removed, takes effect only at a restart, and a
  renamed jar breaks the reload: Magnus's Rider, started with 0.2.10's `RiderTerminalSymbolLinker-0.2.10.jar`, reloaded
  "0.2.10" from that missing jar at the first deploy of the unversioned one, and every extension failed with
  `ClassNotFoundException` until he restarted (2026-11-17). The descriptor still said 0.2.10, though the new jar's
  plugin.xml said 0.2.11. So `sandbox install` restarts the sandbox, and `everyday deploy` says Magnus must restart,
  when a build changes what its plugin.xml registers (`PluginRegistrations`: all of it but the version, change notes
  and description, which the changelog edits).
  Once, right after the sandbox started, a replaced jar wasn't noticed, nor any change after it, until a restart
  (2026-11-17): not seen since, and not understood.
- **A Cloyd of its own:** `sandbox install` publishes this working tree's Cloyd (`CloydBuild`, `Cloyd/Cloyd` in Release)
  into `cloyd\bin` in the sandbox's folder (`InstalledCloyd`), and `CLOYD_FOLDER` names that folder in the sandbox
  Rider's environment: so Claude Sessions there runs the sandbox's `Cloyd.exe --project` as a project opens, and the
  application it starts serves the sandbox's folder, with the sandbox's hub, apart from Magnus's Cloyd. A new build of
  Cloyd ends the application that runs the old one, and has Claude Sessions show the projects again, through a reload of
  the plugin, which runs its startup again. The application outlives the sandbox Rider, as Magnus's outlives his IDEs;
  `sandbox stop`, and the end of a specification run that started the sandbox, close its windows. Its windows go where
  the sandbox Rider's go, after opening on the virtual desktop that's shown, as Rider's do. `sandbox cloyd` tells what
  it shows. The sandbox Rider runs in no job, as the tools start it, where Magnus's IDEs run in the one explorer starts
  them in (2026-11-20): Cloyd's breaking away from an IDE's job is specified in Cloyd's own specifications.
- **JVM options of its own:** `rider64.exe.vmoptions` in the sandbox's folder, which `RIDER_VM_OPTIONS` names, holds
  `-Xmx2048m`, the installation's default heap. Without it, Rider's launcher reads the everyday Riders'
  `%APPDATA%\JetBrains\Rider2026.3\rider64.exe.vmoptions` wherever `idea.config.path` points, and the sandbox ran with
  Magnus's 16 GB heap and Toolbox's options (found 2026-11-18, while the machine's freezes were investigated). The
  launcher reads the installation's `bin\rider64.exe.vmoptions` first, so the file holds only what overrides it. It
  changed little in what the sandbox holds after a run, 4.5 GB of working set against 4.6 GB: it limits how far the
  frontend can grow.
- **Ports:** the MCP server listens on 23861 there. Rider 2026.3's second backend process,
  `Rider.LightweightBackend.exe`, loads that plugin too, and its server takes the next free port, empty: not intended,
  and on the MCP plugin's backlog. So the observation channels are on 23871 and 23872, well away.
- **No trace of the Claude Code session the tools run in:** the sandbox inherits the tools' environment, and a Claude
  Code session sets variables for its children, `CLAUDE_CODE_CHILD_SESSION` and its session id among them. A Claude Code
  started in the sandbox then took itself for a child session, and neither recorded its session nor saved its
  transcript. So the sandbox starts without the `CLAUDE*` variables that aren't set for the user or the machine.
- **A stand-in for Claude Code:** the tooling owner's, which the hub's own specifications run too, published as
  `~/.claude/bin/stand-in-for-claude-code` (its source in `~/.claude/src/StandInForClaudeCode`), is copied with the
  sandbox's hub, and first on the sandbox Rider's `PATH`: see the sandbox's hub below. It has a `CLAUDE_CONFIG_DIR` of
  its own, the Claude Code folder the sandbox's hub serves, records its
  session and keeps its transcript where Claude Code does, and understands `--name`, `--resume`, `/clear`, `/rename`
  and `/exit`. It draws an input box, which takes keys as Claude Code's does, and which the hub types into, and answers
  each prompt with `Heard: <the prompt>`; `Be busy for <n> seconds` keeps it busy. Above
  the box it says which session it runs, each prompt as `> <the prompt>`, and the size of its terminal as it starts and
  each time it changes, the size Claude Code would draw itself at. So specifications run in seconds,
  cost nothing, and leave nothing in Magnus's `~/.claude`. Cloyd starts its sessions' shells in an environment of
  their own, which hasn't the stand-in on its `PATH` until the hub puts it there (the tooling owner, 2026-11-20): a
  session started in the sandbox's Cloyd would run the real Claude Code meanwhile. Until 2026-11-19 the sandbox ran a
  fake of its own, `tools/FakeClaudeCode`, with no input box.
- **No MCP Server banner above a terminal tab:** the MCP Server plugin's banner, which asks to enable the IDE's MCP
  server when `claude` starts in a tab, is dismissed in the sandbox's settings (`SandboxRiderSettings`), as Magnus's
  Riders, with the server enabled, don't show it. It takes three rows from the tab without a resize of the shell, so
  the specification of the size Claude Code draws at failed for it.
- **Nothing waits for a human:** the sandbox exits without confirming, and terminates terminal processes as it exits
  without asking (`processCloseConfirmation`): the terminal's dialog about Claude Sessions' running tabs blocked its
  exit.

## The observation channels

An HTTP server inside each plugin, that listens only when a system property names its port, which only the sandbox
sets. Each request runs on a thread of its own: on the server's one thread, a request waiting for the EDT deadlocked a
reload of the plugin, whose unloading stops the server on the EDT.

- **The symbol linker's,** `dev.magnus.csl.observation.ObservationChannel` on 23871, tells the indexes' state, the links
  the plugin's filters make in a line, and what a click does: which file opened, where the caret and selection are, or
  which rows a picker shows. The links report what a click did through `LinkClickOutcomes`, which nothing listens to
  outside the sandbox. The `sandbox state|links|click|picker` commands call it.
- **Claude Sessions',** `dev.magnus.claudesessions.observation.ObservationChannel` on 23872, tells when its build
  started and the projects open, and has the IDE look at its plugins folder, for a reload. What the plugin has Cloyd
  show, the tools read from the sandbox's Cloyd's own local API: `sandbox cloyd`. Until 0.2.0 it drove the tool window,
  its tabs, the chat, the New Claude Session popup and the session history picker, through `sandbox sessions` and
  `sandbox chat` commands, which went with them. Its chat's pictures rendered the transcript in a Compose scene of its
  own: the sandbox's window is on a virtual desktop of its own, where Compose draws nothing, so painting the panel gave
  an empty picture.

## The specifications

**Cloyd's:** `pwsh Cloyd/run-the-specifications.ps1` builds once, deals the specification classes out over six
`dotnet test` runs at once (`-Runs`), by the time each took when last measured, and says what passed, what failed and
how long each run took; `-ShowTimes` adds each class's time, `-Filter History` takes only some classes. A run is one
process, whose Avalonia UI thread and keyboard focus take its specifications one after another, and each scenario has a
sandbox of its own, its hub included, so the runs share nothing else (`Cloyd.Specifications/TheSandbox`). On
2026-11-20, with each sandbox started ahead and ended in the background: 124-128 s in one run, 31-41 s in six.

**Where a specification's time goes: NCrunch's MCP** (`ncrunch`, Magnus's suggestion, 2026-11-20): the NCrunch Console
Tool's server in its headless mode, whose `StartSession` runs an engine of its own, apart from Rider's NCrunch, so it
needs no IDE; it runs only once started, which nothing does at logon yet. The user-level skill `profiling-dotnet` has
the route and its quirks. It runs the tests in processes of its own, never two at once in one, as these specifications' one UI thread a process needs, and records
the time of every line each test ran, with no profiler to attach: `StartSession` on `Cloyd.slnx` with the engine mode
`Run all tests manually`, `SetTestSelection`, `RunTests`, `WaitForEngineWorkCompleted`, then `GetLinePerformance` for
the tests selected. It ran all 204 in 25-37 s, and showed at once that 12 of a scenario's 12.3 s were its two 3 s waits.
Two limits: NCrunch builds Cloyd, a WinExe, without its `Cloyd.exe`, so the 5 facts that run it fail there, and
`run-the-specifications.ps1` stays the full check; and NCrunch ends its pooled test processes without the run's end,
whose sandboxes' hubs end with them (`--ending-with`), while their folders wait for the next run's first sandbox.

**The plugins':** `dotnet test tests/RiderTerminalSymbolLinker.Specifications` checks what the symbol linker links and what a click does,
in the sandbox, against the fixture solution, and, in `ClaudeSessions/`, what Claude Sessions does: has the sandbox's
Cloyd show each project's window as it opens, and lists the IDE's windows and restarts it through its local API, Cloyd's
application running on through the restart (`ClaudeSessionsInTheSandbox`). Each story starts with the fixture solution
open, and Cloyd showing it. A run starts a stopped sandbox, in about half a minute, and stops it again at its
end: the sandbox holds about 4.6 GB of Magnus's memory, and he wants it off when it isn't in use (2026-11-17). A
sandbox that `sandbox start` started is left running, and gets the working tree's build reloaded, so a run takes
seconds: start it so for a stretch of work, and `sandbox stop` it when the work is done. Every click starts with no
editor open.

**One suite of sandbox Riders at a time, machine-wide:** a run holds the machine's sandbox turn, `SandboxTurn`, from
before it installs the build until its sandbox has stopped, and `sandbox start`, `install` and `stop` hold it while they
run. The .NET Analysis&Refactoring MCP's specifications and tools take the same turn, so each waits while the other
runs, and says who holds it. Several suites at once, beside Magnus's IDEs, froze his machine (2026-11-17). The turn is
`%LOCALAPPDATA%\SandboxRiders\turn.lock` held open, which the operating system closes when its process dies: both
repositories' copies of `SandboxTurn` must open it the same way. A sandbox that `sandbox start` left running doesn't
hold it.

**Don't pipe a run's output into a command that reads to its end,** such as `| Select-Object -Last 5` or `| tail`. When
the run's Gradle build starts a new Gradle daemon, that daemon and the Kotlin compile daemon it starts inherit the run's
output handle, and the pipe stays open until they exit, hours later: on 2026-11-18 a run that passed in 66 seconds held
its pipe until both daemons were stopped. A build that finds a daemon running doesn't start one, which is why it
happens only sometimes. They don't yet cover the terminal itself: whether it applies the filters, and
re-highlights old output.

## The sandbox's Claude Code, git and local API

- **The sandbox's hub** (`SandboxHub`), which the sandbox's Cloyd follows for the Claude Codes that run, and which
  starts the session hosts its sessions run in: the tooling's own sandbox hub, which
  `~/.claude/bin/ClaudeTooling.exe hub sandbox start <folder>` runs in `hub` in the sandbox's folder, from a copy of
  the published build with its stand-in for Claude Code, as the hub's contract has it in "A sandbox's hub". The
  command prints where the hub's parts are, the contract's `SandboxHubStarted`, which the tools compile in: its Claude
  Code folder, its announcement, its log, the stand-in's folder, and the three variables that name them, which the
  sandbox Rider gets too, and through it its Cloyd. The tools put the stand-in's folder first on the sandbox Rider's `PATH` themselves, as the
  contract asks of a client. For its Rider restart, `CLAUDE_SESSIONS_LOCAL_API_FOLDER` names the sandbox Rider's
  announcements, the only IDEs it restarts, and `DOTNET_ANALYSIS_AND_REFACTORING_MCP_PORT` the sandbox's Royder: the
  command starts the hub in the tools' environment, which has both. A hub that runs with other values of the variables
  it serves by is ended and started anew by the command. It's run as the sandbox starts, before each install into a
  running one, taking the published build anew when there is a newer one, and once in any tools process that needs a
  part of it. It stops 5 minutes after the last stream of it closes; `hub sandbox stop` ends it at once,
  with its session hosts and stand-ins. `sandbox status` says where it announces itself. Until 2026-11-20 the tools
  copied and started the hub themselves. `When_the_hub_restarts_rider` has it restart the sandbox
  Rider through `POST /rider-restart`, with `askedByMagnus`, so it shows Magnus no prompt: an agent's
  `ClaudeTooling.exe rider restart` shows him one while a Rider's window is Windows' foreground window, as the sandbox
  Rider's may be once it has started, or his own. Its steps are in `rider-restarts.log` in the sandbox's
  Claude Code folder.
- **The sandbox's session hosts outlive it and its Cloyd,** as Magnus's do his IDEs, their shells in the fixture
  solution's copy:
  `sandbox stop`, and the end of a specification run that started the sandbox, end them with the hub, through
  `hub sandbox stop` (`SandboxRider.StopAndEndItsSessions`), and a start that copies the fixture solution afresh,
  which their shells would hold, ends them through the hub's local API. Their log is `session-hosts.log` in the sandbox's Claude Code folder.
- **The stand-in's record** says `idle` or `busy`, with the time, and `interactive`: the hub leaves out a record with no
  status. It has `procStart` too, the process's creation time as a Windows FILETIME, exact to the 100 nanoseconds as
  Claude Code writes it, which the hub checks against the running process from its step 2 on: a mismatch there drops
  the sandbox's records. The hub tells of a Claude Code about a tenth of a second after it writes its record.
- **The stand-in** writes its transcripts in Claude Code's format, the entries Claude Sessions and the hub read: a
  `user` entry with its folder, Claude's reply and a `last-prompt` for each prompt, an `ai-title` after the first, and
  a `custom-title` for `/rename <name>`, and for `--name <name>` as it starts. `--resume` finds the transcript in any
  project's folder, as Claude Code does. The sandbox's hub copies a newly published stand-in when it next runs, moving
  the files in use aside, so a running stand-in runs on.
- **`GIT_CEILING_DIRECTORIES` is the sandbox's folder:** the sandbox is in Magnus's home folder, a git repository, which
  git would otherwise take the fixture solution for part of. A story that needs git makes the solution a repository of
  its own, and deletes it after.
- **The sandbox's Claude Sessions announces its local API in a folder of its own,** which
  `dev.magnus.claudesessions.localApi.announcements` names in its `idea.properties`: so that Magnus's tools and IDEs
  don't take it for one of theirs, and a story can stand in another IDE there (`AnotherIdeStandIn`).
- **A story can crash the sandbox Rider, or have it restart itself:** `SandboxRider.Kill` ends its process tree, as a
  crash would, and `SandboxRider.FollowItsRestart` follows a restart Rider makes itself, through Claude Sessions'
  `POST /restart`, to the process whose local API announces itself next.
- **A story can open more solutions:** `SandboxRider.AnotherCopyOfTheFixtureSolution` copies the fixture solution into
  a folder of its own, and `OpenAlso` hands it to the running sandbox, which opens it in a window of its own
  (`confirmOpenNewProject2` is "new window" in its settings), and Cloyd shows it too. Stop the sandbox, and its Cloyd's
  application, before deleting the copies, or the sandbox opens them again at its next start.

## Deploying to Magnus's IDEs

`everyday deploy` puts each plugin's working-tree build in the plugins folder of every installed IDE it runs in, such as
`%APPDATA%\JetBrains\Rider2026.3\plugins`, moving the mapped jar aside: the symbol linker in Rider, Claude Sessions in
Rider, IntelliJ IDEA and PyCharm. All three run with `-Didea.auto.reload.plugins=true`, in the `.vmoptions` in each
one's folder under `%APPDATA%\JetBrains` (Rider since 2026-11-17, IDEA and PyCharm from their restart after that), so
each reloads a plugin without a restart when it next looks at its plugins folder, expected when he switches to its
window. The deploy says for each IDE whether it does, or needs a restart: a plugin new to an IDE always does.

**Every deploy puts the working tree's Cloyd in first,** which Claude Sessions runs from 0.2.0: published in Release
into `%LOCALAPPDATA%\Cloyd\bin` (`InstalledCloyd.Everyday`), moving the files a running Cloyd application maps aside, as
for a plugin's jar. The application that runs runs on with the build it has until its last window closes; the next
project to open then starts the new one. `--dry-run` says which, and changes nothing. `everyday reload Cloyd` has it
take the new build at once: it closes its windows, each saving its tabs, and runs `Cloyd.exe --project` for each
project it showed, which starts it again with the same windows, their tabs brought back. The sessions run on in their
hosts. On Magnus's go, as it closes the windows he works in. `everyday cloyd` reads where its windows are, changing
nothing: each one's virtual desktop, and its project's IDE window's, which it is to keep to (`CloydBesideTheIdes`).

**What links in a line of a terminal's output,** with a project's files as git lists them now and its C# symbols as
Magnus's Rider has them now, with no window: `Cloyd.exe --links-in <the project's folder> "<line>"`, from Cloyd's
`bin`, Debug or the installed one. It prints how long reading each took, and each file reference and symbol reference,
with its span; it changes nothing. On 2026-11-20 it read this repository's 265 files in 48 ms and gathered Cloyd's
solution in 358 ms, and Compze's in 3.5 s.

**A build that needs a restart isn't put into a running IDE that reloads,** and the deploy says `DIDN'T DEPLOY`, and
`--dry-run` says `CLOSE <IDE> FIRST`. Such a build adds or removes a file, or changes what its plugin.xml registers, and
the IDE reloads it as it does any changed jar, at once, but keeps the registrations it read as it started. On 2026-11-19
a Claude Sessions build whose action had moved to another package went into Rider and IntelliJ IDEA so: both reloaded it
within seconds, didn't find the action's old class, and the tool window broke as it opened, with Magnus's sessions left
parked in the Terminal tool window. A build whose plugin.xml was again what they had read at their start fixed it, by a
reload. Whether an IDE runs is told by the process its system folder's `.pid` names, such as
`%LOCALAPPDATA%\JetBrains\Rider2026.3\.pid`, which the IDE leaves behind when it exits. `everyday deploy
--into-running-ides` puts such a build into a running IDE all the same, for when Magnus restarts it right after: the
plugin is broken in it until he does.

An agent runs in one of those IDEs, and ends with it, so it can't run such a deploy itself: `everyday deploy
--once-the-ides-close` says what the deploy would do, and has it run once Magnus has closed his IDEs
(`DeployOnceTheIdesClose`). It waits in a one-shot scheduled task, which closing them doesn't end, and which runs in his
desktop session: its console window says what it waits for, and a message, when it's done, to start them again. The
task runs a copy of the tools in `bin/deploy-once-the-ides-close`, so that building them meanwhile isn't refused, and
removes itself when done. It's set up only on Magnus's go-ahead, as any deploy that needs a restart: closing his IDEs
for another reason would deploy too.

**`everyday deploy --restarting-the-ides` has the hub restart the IDEs, the build going in while each is down**
(`DeployRestartingTheIdes`): Magnus, 2026-11-20, "I greenlight, you install and restart. My IDEs come back just as they
were on the same desktops." It builds, puts the builds aside with a `CopyOfTheTools` (`StagedPluginBuilds`, under
`%LOCALAPPDATA%\RiderTerminalSymbolLinkerDevelopment\staged-plugin-builds`), deploys at once into the IDEs that don't
run, and asks the published hub, `POST /ide-restart` (`HubsLocalApi`), to restart those that run and lack the build,
as Magnus asks himself, so with no prompt of his. Each IDE's Claude Sessions runs the hub's `beforeItStartsAgain`,
`everyday deploy --into-the-ides-that-closed`, once the IDE has exited, and starts the IDE when it has ended (Claude
Sessions' `RestartRunningACommand`): it deploys the staged builds into every IDE that doesn't run, taking turns with the
other IDEs' through a Compze `IMutex`, and logs to `deploys-restarting-the-ides.log` beside them. The command follows
the hub's `GET /ide-restart` until every IDE is back, and prints its steps, its outcome and the log's new lines. The
agent that runs it survives the restart, in its session host. A build that changes no registration still goes in by
`everyday deploy`, a reload. The running IDEs need a Claude Sessions that knows `beforeItStartsAgain`, which this deploy
can't bring itself: an older one restarts without the command, and the IDE comes back with the build it had. The hub's
contract is its "Restarting the IDEs"; `When_the_hub_restarts_the_ide_for_a_deploy` runs such a restart of the sandbox
Rider through the sandbox's hub, with a command that records that it ran between the exit and the start.

**`everyday reload <plugin> [<IDE>]` reloads the build an IDE has,** to see in Magnus's own IDEs what a reload does,
such as whether what it shows comes through it. It gives the plugin's jars the time of now, as `sandbox reload` does in the
sandbox, which the IDE takes for a new build. On 2026-11-18 his Rider reloaded within 13 seconds, both times. The next
`everyday deploy` puts the build's own times back, and so reloads the plugin once more, even if the build is the same.

**A plugin installed from disk has no update source,** and IntelliJ IDEA 2026.3, at least, warns as it starts that it
can't tell where the plugin came from ("Cannot update plugin"). So the deploy names the Marketplace as its source in the IDE's
`options\pluginUpdateSources.xml` where none is named, as the IDE does for a plugin installed from there: it finds no
update there for an unpublished plugin, and finds the published ones. The IDE writes that file only when it changes a
source itself, so an entry written while it runs survives its exit (tried in the sandbox, 2026-11-17).

**Install Plugin from Disk always restarts Rider** when an earlier version is installed, whatever the plugin is: read
from the bytecode of `PluginInstaller.installFromDisk` in Rider 2026.3. Auto-reload and Marketplace updates are the
routes that reload a running plugin. Auto-reload reacts to every plugin's folder, the MCP plugin's included, which can't
be reloaded and asks for a restart.

## Codex helpers, in checkouts of their own

A Claude session hands work to Codex sessions, and reviews and merges what they do, as Magnus asked on 2026-11-22: "Please
do what you need to do to work with codex helpers and then tell me how to get the helpers started in those new
checkouts". Each helper works in a git worktree of its own, on a branch of its own, since builds of the same projects in
one checkout collide, and other sessions work in the main one. The rule on working in a checkout other than the main one
says what a helper's checkout can't do: deploy, or edit through Rider's and ReSharper's tools, which see the main
checkout.

**The checkouts,** made 2026-11-22 from `main`: `C:\Dev\ClaudeSymbolLinker.worktrees\codex-helper-1` and `-2`, on the
branches `codex-helper-1` and `-2`. To make another:
`git worktree add -b codex-helper-3 C:/Dev/ClaudeSymbolLinker.worktrees/codex-helper-3 main`, then in it
`sh .claude/attach-shared-claude-config.sessionstart-hook.sh`, which gives it the shared catalog, and
`~/.claude/bin/ClaudesSetupForCodex.exe project <its folder>`, which writes Codex's
rules, skills and MCP servers there (`AGENTS.override.md`, `.agents/skills`, `.codex/config.toml`, all ignored by git).
Run that again after a rule changes. Codex asks Magnus to trust the folder the first time he starts it there, and loads
the folder's `.codex/config.toml` only once he has.

**Starting a helper** is Magnus's: `cd` into the checkout, `codex`, trust the folder, then `/rename codex-helper-1`, the
name it is addressed by. It shows in `list_peers` once it has had a turn.

**Handing it a task,** the coordinating session, the one that hands the work:

1. Bring its branch up to `main` while it has nothing unmerged: `git -C <its checkout> merge --ff-only main`.
2. Brief it with `send_to_peer`, a brief that stands alone: the task, the documents and code to read, what is done
   (committed on its branch, its specifications passing, the changelog entry where one is due), what not to touch, and
   what to send back: its commits, the specifications it ran and their result, and what it noticed and left.
3. Review its commits once it replies: `git log main..codex-helper-1`, `git diff main...codex-helper-1`. Ask for changes
   with `send_to_peer`, or make them.
4. Merge: rebase its branch onto `main` if `main` has moved, then `git merge --ff-only codex-helper-1` in the main
   checkout. Run the full specifications there, deploy, and push.

**What helps it:** the specifications run from its checkout, also while another checkout's run, since each sandbox is
named by its run's process (`Sandbox`). One-shot Codex agents, for a question that needs no checkout of its own, are
`codex exec --ephemeral -s read-only -C <folder> -o <answer file> - < <prompt file>`, in the background; they can't be
continued. Both use the model and effort in `~/.codex/config.toml` (`gpt-6.2-astra` at `xhigh` on 2026-11-22), which
`-m` and `-c model_reasoning_effort=<level>` override. The peers server's own traps, such as a renamed session listed
under its old name, are in `~/.claude/dev_docs/peers-and-codex-backlog.md`: address a helper by `codex:<its id>` when
its name fails.
