# Claude owns the symbol linker's code and the tools it's developed with

Magnus, 2026-11-17: "This is your project in terms of code. You are the technical lead. I just tell you what features
I need so the environment needs are yours."

**Magnus says what the plugin should do for its users. How it's built, and with what, is yours to decide:** the code's
design and structure, and the development environment. Make the calls, and tell him afterwards what changed and why.
The tools are yours as a product, as the [collaboration rules](../01-universal-shared/001-collaboration-rules.md) say
under "The tools you develop with are yours, as a product". Today they are:

- the Gradle build, with the repository's own JDK 21 under `.tooling/`: `test`, `verifyPlugin` (the Marketplace's
  Plugin Verifier, failing on any finding) and `buildPlugin`;
- IDEA's MCP server (`idea` in `.mcp.json`), with this project open in it: inspections, the IDE build, and lookups into
  the classes of the Rider the plugin builds against. The `.idea/` files the repository tracks are its project setup;
- the sandbox Rider, `dotnet run --project tools/RiderTerminalSymbolLinker.DevelopmentTools -- sandbox`, which lists
  its commands: a Rider of its own that runs this working tree's build on
  `tests/RiderTerminalSymbolLinker.FixtureSolution`, with the .NET Analysis&Refactoring MCP build Magnus's Riders run
  unless told to run the Marketplace's, reloads a new build in a second, and tells through the plugin's development
  observation channel what the plugin links and what a click does;
- the specifications that check that in the sandbox, `dotnet test tests/RiderTerminalSymbolLinker.Specifications`, in
  seconds once the sandbox runs;
- `everyday deploy`, the same tool's command that puts the working tree's build in Magnus's Rider, which reloads it
  without a restart;
- the CI workflows under `.github/workflows/`;
- the unit tests under `src/test/`.

What you add to develop or verify the plugin joins the list.

**What stays Magnus's:**

- **What the plugin does for its users:** what it links, what a click does, what it shows, and which Riders and which
  MCP plugin it requires. A change there is a feature, agreed with him first. Code added for development must change
  nothing a user could notice; if you can't tell whether one could, ask.
- **Publishing:** a release tag publishes to the Marketplace, so it waits for his word, as
  [changing an external environment](../01-universal-shared/003-never-change-external-environments-without-approval.md)
  always does.
