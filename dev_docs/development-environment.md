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
`RIDER_PROPERTIES`, which runs the working tree's build on a copy of `tests/FixtureSolution`. Its window goes on IDEA's
virtual desktop, on the other monitor.

- **Plugins of its own:** `sandbox install` builds with Gradle's `prepareSandbox` and copies our plugin into the
  sandbox's plugins folder, and the .NET Analysis&Refactoring MCP's plugin only while the sandbox is stopped. Loading
  them straight from Gradle's folder kept Gradle from building while the sandbox ran: its .NET backend holds the MCP
  plugin's DLL open.
- **Reloading without a restart:** the sandbox runs with `idea.auto.reload.plugins`. Windows won't overwrite a jar a
  running Rider maps into memory, but lets it be moved, so the old jar is moved aside first and deleted by a later
  install. Rider notices changed plugin files only when it looks at its plugins folder, which nothing watches, so
  `install` asks it to look through the observation channel. A reload takes about a second; the indexes come back, since
  the startup activities run again.
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
sandbox, against the fixture solution. A running sandbox gets the working tree's build reloaded, so a run takes seconds.
Every click starts with no editor open. They don't yet cover the terminal itself: whether it applies the filters, and
re-highlights old output.

## Deploying to Magnus's Rider

`everyday deploy` puts the working tree's build in `%APPDATA%\JetBrains\Rider2026.3\plugins`, moving the mapped jar
aside. His Rider runs with `-Didea.auto.reload.plugins=true` (since 2026-11-17), so it reloads the plugin without a
restart when it next looks at its plugins folder, expected when he switches to its window.

**Install Plugin from Disk always restarts Rider** when an earlier version is installed, whatever the plugin is: read
from the bytecode of `PluginInstaller.installFromDisk` in Rider 2026.3. Auto-reload and Marketplace updates are the
routes that reload a running plugin. Auto-reload reacts to every plugin's folder, the MCP plugin's included, which can't
be reloaded and asks for a restart.
