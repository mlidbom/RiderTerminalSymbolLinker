# A fix or feature is done when its specification passes in the sandbox, and it's deployed to Magnus's Rider

- **Specify it:** add a specification of the change to `tests/RiderTerminalSymbolLinker.Specifications`, and run
  them all: `dotnet test tests/RiderTerminalSymbolLinker.Specifications`, which reloads the working tree's build in
  the sandbox Rider first. Every one passes, or is skipped with the known bug it names.
- **Record it in the changelog,** as [the changelog rule](changelog.md) says.
- **Deploy it:** `dotnet run --project tools/RiderTerminalSymbolLinker.DevelopmentTools -- everyday deploy`, and tell
  Magnus that his Riders reload it when he next switches to them, without a restart.
- **A zip only when he asks for one:** the `buildPlugin` Gradle task puts it at
  `build/distributions/RiderTerminalSymbolLinker-<pluginVersion>.zip`. Gradle needs `JAVA_HOME` to be the repository's
  own JDK 21: `$env:JAVA_HOME = "$PWD\.tooling\jdk\jdk-21.0.11+10"; .\gradlew.bat buildPlugin`.
