# A fix or feature is done when its specification passes in the sandbox, and it's deployed to Magnus's Rider

- **Specify it:** add a specification of the change to `tests/RiderTerminalSymbolLinker.Specifications`, and run
  them all: `dotnet test tests/RiderTerminalSymbolLinker.Specifications`, which reloads the working tree's build in
  the sandbox Rider first. Every one passes, or is skipped with the known bug it names.
- **Stop the sandbox if you started it** with `sandbox start`: it holds about 4.6 GB, and Magnus wants it off when
  it isn't in use. A specification run stops one it started itself.
- **Record it in the changelog,** as [the changelog rule](changelog.md) says.
- **Deploy it:** `dotnet run --project tools/RiderTerminalSymbolLinker.DevelopmentTools -- everyday deploy`, and tell
  Magnus that his Riders reload it when he next switches to them, without a restart.
- **A zip only when he asks for one:** `.\build.ps1`, which runs Gradle's `buildPlugin` with the repository's own JDK
  21, puts it at `build/distributions/RiderTerminalSymbolLinker-<pluginVersion>.zip`.
