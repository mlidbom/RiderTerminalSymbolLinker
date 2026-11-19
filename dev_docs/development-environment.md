# The development environment

What the plugins are built, checked and deployed with: the symbol linker, and Claude Sessions
([how it works](claude-sessions.md)). The commands are in
`dotnet run --project tools/RiderTerminalSymbolLinker.DevelopmentTools`, which lists them.

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
- **A fake Claude Code:** `tools/FakeClaudeCode`, built with the tools as `claude.exe`, is copied into the sandbox's
  folder as it starts, and first on its `PATH`: run from the build, it was held open by the sandbox's tabs, and the
  tools couldn't build while the sandbox ran. It has a `CLAUDE_CONFIG_DIR` of its own, `claude-code` in the sandbox's
  folder, records its session and keeps its transcript where Claude Code does, and understands `--resume`, `/clear` and
  `/exit`. So Claude Sessions' specifications run in seconds, cost
  nothing, and leave nothing in Magnus's `~/.claude`.
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
- **Claude Sessions',** `dev.magnus.claudesessions.observation.ObservationChannel` on 23872, tells the view's tabs, the
  process and session each runs, the saved sessions, and the Terminal tool window's tabs; it starts sessions, drives the
  New Claude Session popup and the session history picker, chooses the shell the terminals run, types into a tab, shows
  what a tab's terminal shows, and closes tabs. It closes a tab by ending its process, as Claude Code's exit
  does: the close button, and the terminal API's `closeTab`, ask the user to confirm. The `sandbox sessions` commands
  call it.

## The specifications

`dotnet test tests/RiderTerminalSymbolLinker.Specifications` checks what the symbol linker links and what a click does,
in the sandbox, against the fixture solution, and, in `ClaudeSessions/`, what Claude Sessions does with the fake Claude
Code: a new session, `/clear`, an IDE restart, a plugin reload, and Claude Code exiting. A run starts a stopped sandbox, in about half a minute, and stops it again at its
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

- **The fake Claude Code** writes its transcripts in Claude Code's format, the entries Claude Sessions reads: a `user`
  entry with its folder and a `last-prompt` for each line typed, an `ai-title` after the first, and a `custom-title`
  for `/rename <name>`, and for `--name <name>` as it starts. `--resume` finds the transcript in any project's folder, as Claude Code does. The sandbox takes
  a copy of its build as it starts, so a change to it takes a restart of the sandbox.
- **Each story starts with no history, and in PowerShell 7:** `TheClaudeSessionsView` deletes the sandbox's
  transcripts, and has the terminals run `pwsh.exe`, Magnus's shell, as a story may have chosen another. A story writes the
  transcripts it needs with `SessionTranscript`, dated as it likes.
- **`GIT_CEILING_DIRECTORIES` is the sandbox's folder:** the sandbox is in Magnus's home folder, a git repository, which
  git would otherwise take the fixture solution for part of. A story that needs git makes the solution a repository of
  its own, and deletes it after.
- **The sandbox's Claude Sessions announces its local API in a folder of its own,** which
  `dev.magnus.claudesessions.localApi.announcements` names in its `idea.properties`: so that Magnus's tools and IDEs
  don't take it for one of theirs, and a story can stand in another IDE there (`AnotherIdeStandIn`).

## Deploying to Magnus's IDEs

`everyday deploy` puts each plugin's working-tree build in the plugins folder of every installed IDE it runs in, such as
`%APPDATA%\JetBrains\Rider2026.3\plugins`, moving the mapped jar aside: the symbol linker in Rider, Claude Sessions in
Rider, IntelliJ IDEA and PyCharm. All three run with `-Didea.auto.reload.plugins=true`, in the `.vmoptions` in each
one's folder under `%APPDATA%\JetBrains` (Rider since 2026-11-17, IDEA and PyCharm from their restart after that), so
each reloads a plugin without a restart when it next looks at its plugins folder, expected when he switches to its
window. The deploy says for each IDE whether it does, or needs a restart: a plugin new to an IDE always does.

**`everyday reload <plugin> [<IDE>]` reloads the build an IDE has,** to see in Magnus's own IDEs what a reload does,
such as whether his tabs come through it. It gives the plugin's jars the time of now, as `sandbox reload` does in the
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
