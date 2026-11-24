# A fix or feature is done when its specification passes in the sandbox, and it's deployed to Magnus's Rider

- **Specify it:** add a specification of the change to `tests/RiderTerminalSymbolLinker.Specifications`, and run
  them all: `dotnet test tests/RiderTerminalSymbolLinker.Specifications`, which reloads the working tree's build in
  the sandbox Rider first. Every one passes, or is skipped with the known bug it names.
- **Stop the sandbox if you started it** with `sandbox start`: it holds about 4.6 GB, and Magnus wants it off when
  it isn't in use. A specification run stops one it started itself.
- **Record it in the changelog,** as [the changelog rule](changelog.md) says.
- **In a checkout other than the main one,** such as a Codex helper's, done is committed with its specifications
  passing, and the session that handed the work deploys it: see the rule on working in a checkout other than the main
  one.
- **Deploy it:** `dotnet run --project tools/RiderTerminalSymbolLinker.DevelopmentTools -- everyday deploy`, and tell
  Magnus what it says for each IDE: that it reloads the plugin when he next switches to it, or that he must restart it.
- **Deploy and restart without asking,** the IDEs included, unless you have good reason to believe something will
  break for him (Magnus, 2026-11-24: "Just deploy and restart. Cloyd, the hub, rider. Just restart what needs
  restarting."). `everyday deploy --dry-run` builds and says what the deploy would do, marking a restart `RESTART`;
  `ClaudeTooling.exe ides restart` restarts an IDE, and shows him a prompt he can say no to.
- **A zip only when he asks for one:** `.\build.ps1`, which runs Gradle's `buildPlugin` with the repository's own JDK
  21, puts the symbol linker's at `build/distributions/RiderTerminalSymbolLinker-<pluginVersion>.zip`, and Claude
  Sessions' at `ClaudeSessions/build/distributions/ClaudeSessions-<version>.zip`.
