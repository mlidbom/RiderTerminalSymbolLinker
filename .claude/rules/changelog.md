# Record every change in the changelog

Whenever you add a feature, fix a bug, or make any user-facing or otherwise notable change, add an entry
to the `[Unreleased]` section of [CHANGELOG.md](../../CHANGELOG.md) as part of the same change — not as a
follow-up. **A change isn't done until it's in the changelog.**

- Follow the existing [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format already in the file:
  group entries under `### Added`, `### Changed`, `### Fixed`, `### Removed`, `### Deprecated`, or
  `### Security`. Create the `## [Unreleased]` section if it's missing — entries accumulate there until a
  release cuts the next version.
- Match the established prose style: a **bold one-line summary**, then the behavior — what works now, the
  notable edge cases, and how it differs from before. Write for a plugin user, describing behavior, not
  implementation.
- Pure-internal changes with no observable effect (refactors, test-only changes, build tweaks) don't need
  an entry — but when in doubt, add one.
