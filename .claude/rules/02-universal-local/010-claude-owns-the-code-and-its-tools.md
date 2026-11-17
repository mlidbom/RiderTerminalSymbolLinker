# Claude owns both plugins' code and the tools they're developed with

Magnus, 2026-11-17: "This is your project in terms of code. You are the technical lead. I just tell you what features
I need so the environment needs are yours." That was said of the symbol linker; asked the same day whether it covers
Claude Sessions too, he said yes.

**Magnus says what each plugin should do for its users. How it's built, and with what, is yours to decide:** the code's
design and structure, and the development environment. Make the calls, and tell him afterwards what changed and why.
The tools are yours as a product, as the [collaboration rules](../01-universal-shared/001-collaboration-rules.md) say
under "The tools you develop with are yours, as a product". Today they are:

- the Gradle build, with the repository's own JDK 21 under `.tooling/`: `test`, `verifyPlugin` (the Marketplace's
  Plugin Verifier, failing on any finding) and `buildPlugin`;
- IDEA's MCP server (`idea` in `.mcp.json`), with this project open in it: inspections, the IDE build, and lookups into
  the classes of the Rider the plugins build against. The `.idea/` files the repository tracks are its project setup;
- the sandbox Rider, `dotnet run --project tools/RiderTerminalSymbolLinker.DevelopmentTools -- sandbox`, which lists
  its commands: a Rider of its own that runs this working tree's builds on
  `tests/RiderTerminalSymbolLinker.FixtureSolution`, with the .NET Analysis&Refactoring MCP build Magnus's Riders run
  unless told to run the Marketplace's, and a fake Claude Code (`tools/FakeClaudeCode`). It reloads a new build in a
  second, and tells through each plugin's development observation channel what the plugin does;
- the specifications that check that in the sandbox, `dotnet test tests/RiderTerminalSymbolLinker.Specifications`, in
  seconds once the sandbox runs;
- `everyday deploy`, the same tool's command that puts the working tree's builds in Magnus's IDEs, which reload them
  without a restart where they run with `-Didea.auto.reload.plugins=true`;
- the CI workflows under `.github/workflows/`;
- the symbol linker's unit tests under `src/test/`.

What you add to develop or verify the plugins joins the list.

**What stays Magnus's:**

- **What each plugin does for its users:** what the symbol linker links, what a click does, what it shows, and which
  Riders and which MCP plugin it requires; what Claude Sessions does with the sessions, what it shows, and which IDEs it
  runs in. A change there is a feature, agreed with him first. Code added for development must change nothing a user
  could notice; if you can't tell whether one could, ask.
- **Publishing:** a release tag publishes to the Marketplace, so it waits for his word, as
  [changing an external environment](../01-universal-shared/003-never-change-external-environments-without-approval.md)
  always does.
