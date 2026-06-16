# Build the plugin and report its location when done

After finishing any fix or feature, **build the installable plugin and tell Magnus where the artifact is**,
so he can install it in Rider via *Settings → Plugins → ⚙ → Install Plugin from Disk…*. This is the last
step of feature/fix work, after code, tests, and the [changelog](changelog.md) entry.

- Build with the `buildPlugin` Gradle task. Building requires `JAVA_HOME` to be the repo JDK 21 (see the
  [repo-local JDK note](../../../../.claude/projects/c--Dev-ClaudeSymbolLinker/memory/repo-local-jdk-location.md)
  — it lives at `.tooling/jdk`). On Windows PowerShell:

  ```powershell
  $env:JAVA_HOME = "$PWD\.tooling\jdk\jdk-21.0.11+10"; .\gradlew.bat buildPlugin
  ```

- The artifact lands at `build/distributions/RiderTerminalSymbolLinker-<pluginVersion>.zip` (the version
  comes from `pluginVersion` in `gradle.properties`). Report that path explicitly.
- If the build fails, say so and stop — don't report a stale zip from a previous build as if it were fresh.
