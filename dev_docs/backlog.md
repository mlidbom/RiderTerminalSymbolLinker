# Backlog: work deferred, and what waits to be passed on

What Magnus has deferred, and what waits for a time or an event before it goes on to another repository. Each entry
says what it is, why it waits, and what to do once it can go ahead. Remove an entry once it's done.

Cloyd's entries went with it to `~/.claude`, on 2026-11-23: `~/.claude/dev_docs/cloyd/backlog.md`, the chat's way to
unfold all of a conversation and Compze.xUnitBDD's scenario run once for all of a class's facts among them.

## Deferred by Magnus

None now.

## Handed to the Codex helpers

Which helper has what, so that two strands don't touch the same code (`dev_docs/development-environment.md`, "Codex
helpers, in checkouts of their own"). Remove an entry once its work is merged. None is handed now.

## Waiting for Compze

None now.

## Deferred by the tooling owner

From the unanswered lists of ended sessions, decided on 2026-11-24 and kept here until a session works on what they
touch.

- **Catch the Plugin Verifier's findings before a push** (ci-failures 2, 2026-11-21): CI was the first place
  `verifyPlugin` ran, and it failed on a deprecation the local Rider (263.7307.325) has too. Not added to the rule on
  when work is done, since every change would wait on the verifier: measure what `./gradlew verifyPlugin` costs, and
  have everyday deploy run it for a plugin it deploys if that is cheap enough.
- **Tell the session that broke CI** (ci-failures 3, 2026-11-21): main's Build run stayed red for about 16 hours and
  10 pushes, and no session noticed. A command in the development tools that reports main's latest Build run and its
  failing step (`gh run list`), run by everyday deploy and before a push.
- **Install a build that needs a restart at the IDE's next start** (cl-tab-restore 25, 2026-11-18): a deploy of such a
  build (a new action, a new dependency) puts the files where the IDE watches them, and the IDE reloads the new classes
  with the old registrations before Magnus can restart it. Hand such a build to the IDE's startup action script
  (`system/plugins/action.script`), as the platform's own installer does for updates that need a restart, specified in
  the sandbox.
