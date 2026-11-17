# The development environment

What the plugin is built, checked and deployed with. The commands are in
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
`RIDER_PROPERTIES`, which runs the working tree's build on a copy of `tests/RiderTerminalSymbolLinker.FixtureSolution`,
the fixture solution. Its window goes on IDEA's virtual desktop, on the other monitor.

- **Plugins of its own:** `sandbox install` builds with Gradle's `prepareSandbox` and copies our plugin into the
  sandbox's plugins folder. Loading it straight from Gradle's folder kept Gradle from building while the sandbox ran:
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
  plugin.xml said 0.2.11.
- **Ports:** the MCP server listens on 23861 there. Rider 2026.3's second backend process,
  `Rider.LightweightBackend.exe`, loads that plugin too, and its server takes the next free port, empty: not intended,
  and on the MCP plugin's backlog. So the observation channel is on 23871, well away.

## The observation channel

`dev.magnus.csl.observation.ObservationChannel`, an HTTP server inside the plugin that listens only when the system
property `dev.magnus.csl.observation.port` is set, which only the sandbox does. It tells the indexes' state, the links
the plugin's filters make in a line, and what a click does: which file opened, where the caret and selection are, or
which rows a picker shows. The links report what a click did through `LinkClickOutcomes`, which nothing listens to
outside the sandbox. The `sandbox state|links|click|picker` commands call it.

## The specifications

`dotnet test tests/RiderTerminalSymbolLinker.Specifications` checks what the plugin links and what a click does, in the
sandbox, against the fixture solution. A run starts a stopped sandbox, in about half a minute, and stops it again at its
end: the sandbox holds about 4.6 GB of Magnus's memory, and he wants it off when it isn't in use (2026-11-17). A
sandbox that `sandbox start` started is left running, and gets the working tree's build reloaded, so a run takes
seconds: start it so for a stretch of work, and `sandbox stop` it when the work is done. Every click starts with no
editor open. They don't yet cover the terminal itself: whether it applies the filters, and
re-highlights old output.

## Deploying to Magnus's Rider

`everyday deploy` puts the working tree's build in `%APPDATA%\JetBrains\Rider2026.3\plugins`, moving the mapped jar
aside. His Rider runs with `-Didea.auto.reload.plugins=true` (since 2026-11-17), so it reloads the plugin without a
restart when it next looks at its plugins folder, expected when he switches to its window.

**Install Plugin from Disk always restarts Rider** when an earlier version is installed, whatever the plugin is: read
from the bytecode of `PluginInstaller.installFromDisk` in Rider 2026.3. Auto-reload and Marketplace updates are the
routes that reload a running plugin. Auto-reload reacts to every plugin's folder, the MCP plugin's included, which can't
be reloaded and asks for a restart.
