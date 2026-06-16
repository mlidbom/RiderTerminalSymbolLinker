# Record every change in the changelog

Whenever you add a feature, fix a bug, or make any user-facing or otherwise notable change, add an entry
to [CHANGELOG.md](../../CHANGELOG.md) as part of the same change — not as a follow-up. **A change isn't
done until it's in the changelog.**

## Which version section to add it to

Do **not** use an `[Unreleased]` section. Entries always go into a concrete numbered version, chosen by
whether the changelog's current (top) version has already been released as a git tag:

1. Read the top version section in CHANGELOG.md and check whether it's tagged: `git tag --list v<version>`.
2. **Not tagged** → that version is the in-progress release. Add the entry to it.
3. **Tagged** → that version is already released. Then:
   - Create a **new** version section above it (next patch bump, e.g. `0.2.1` → `0.2.2`) and add the entry
     there.
   - Add its compare link at the bottom of the file. The in-progress (top) version compares against
     `HEAD` until it's tagged at release: `[0.2.2]: …/compare/v0.2.1...HEAD`.
   - Bump `pluginVersion` in [gradle.properties](../../gradle.properties) to the new version, so the built
     plugin carries that version from then on.

## Format

- Follow the existing [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format already in the file:
  group entries under `### Added`, `### Changed`, `### Fixed`, `### Removed`, `### Deprecated`, or
  `### Security`.
- Match the established prose style: a **bold one-line summary**, then the behavior — what works now, the
  notable edge cases, and how it differs from before. Write for a plugin user, describing behavior, not
  implementation.
- Pure-internal changes with no observable effect (refactors, test-only changes, build tweaks) don't need
  an entry — but when in doubt, add one.
