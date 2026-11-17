# Releasing

**A release is published to the JetBrains Marketplace, so it waits for Magnus's word.**

## How a version is released

- **The version** is `pluginVersion` in `gradle.properties`, with its section in `CHANGELOG.md`: the changelog rule says
  which section new entries go into. The section becomes the Marketplace's "What's new" and the GitHub Release's notes.
- **Pushing a `v<version>` tag releases it:** `.github/workflows/release.yml` runs the Marketplace's Plugin Verifier,
  builds and signs, creates the GitHub Release, and uploads to the Marketplace. A version the Marketplace has already is
  skipped, so a rerun is safe.
- **The verifier fails on any finding,** in CI and before a release: updates whose report had none have been approved
  at once (Magnus, 2026-11-14). It checks the pinned Rider and the latest release; locally, `verifyPlugin` checks the
  installed Rider.
- **Backfilling an old tag:** run the Release workflow by hand from the `main` branch, with the tag in its `tag` input.
  Don't pick the tag in the branch list: the workflow would then be the old tag's, which may not publish.
- **The release run once failed with `ClosedFileSystemException`** while resolving dependencies, before verifying
  anything (0.2.10, 2026-11-17). Rerunning the failed job passed: it was intermittent.

## Which Riders it supports

`sinceBuild` in `build.gradle.kts` follows the earliest Rider the .NET Analysis&Refactoring MCP supports, since a
plugin that claims an older Rider than its dependency can't be installed there: 263 (Rider 2026.3) since 0.2.10.
`platformVersion` in `gradle.properties`, the Rider CI builds and verifies against, is that oldest supported Rider.
There is no `untilBuild`: a cap disabled 0.2.7 on Rider 2026.3 though nothing needed changing. Rider 2026.1 and 2026.2
keep 0.2.9, which the Marketplace goes on offering them.

The build's dependency on the MCP plugin, `plugin("dev.magnus.dotnet-analysis-and-refactoring-mcp", ...)`, lets the
verifier resolve the `<depends>`; it isn't bundled. Keep the two plugins' release setups alike (Magnus, 2026-11-14):
change one, and say whether the other should follow.

## The Marketplace

- **Numeric id 32281** (`plugins.jetbrains.com/plugin/32281`); its versions are at
  `https://plugins.jetbrains.com/api/plugins/32281/updates`.
- **The `<id>` in plugin.xml, `dev.magnus.riderterminalsymbollinker`, can never change:** the listing would be lost.
- **The name is moderated:** an upload that changes `<name>` waits for review, while other updates are approved at
  once. A name with "Rider" in it was refused (0.1.0 to 0.1.5); it became ".NET Terminal Symbol Linker" in 0.2.3.
- **Secrets:** the GitHub secrets `PUBLISH_TOKEN`, `CERTIFICATE_CHAIN`, `PRIVATE_KEY` and `PRIVATE_KEY_PASSWORD` are set.
  The signing material is kept outside the repository, in `C:\Users\magnu\RiderTerminalSymbolLinker-signing\`: never
  commit it. Under Git Bash, openssl's `-subj` needs `MSYS_NO_PATHCONV=1`, or the leading `/CN=` is mangled.
- **`zipSigner()`** is declared in the build, as the Gradle plugin's documentation requires: without it, the MCP
  plugin's identical build found no signer on CI. This build found one without it.
