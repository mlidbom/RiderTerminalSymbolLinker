# Backlog: work deferred, and what waits to be passed on

What Magnus has deferred, and what waits for a time or an event before it goes on to another repository. Each entry
says what it is, why it waits, and what to do once it can go ahead. Remove an entry once it's done.

## Deferred by Magnus

### An easy way to unfold all of a chat's conversation, or chosen parts of it

**Magnus, 2026-11-21,** asked whether the chat's search should open folded tool calls for a match in them: "Skip it. We
will solve it eventually by having an easy way to unfold everything or specific parts of the transcript." The search
looks only in what shows with everything folded (`ShownInTheChat.TextShown`), so it finds nothing in a run's rows nor in
a tool call's command and output.

**Then:** the way to unfold, and the search looking in what is unfolded too: `TextShown` would say the text of what is
open, in the order the view draws it, so that a match's occurrence lands on its line (`MatchesPainted.AsTheOneShown`);
and tool output drawn in `MarkdownTextBlock`s, to be painted.

## Handed to the Codex helpers

Which helper has what, so that two strands don't touch the same code (`dev_docs/development-environment.md`, "Codex
helpers, in checkouts of their own"). Remove an entry once its work is merged.

### codex-helper-1 (`cloyd-codex-1`, `codex:01a2009c-1d49-7f32-8ab7-ed623493aef1`): Shift+Enter in a Codex session's terminal tab

**Handed by .NET-cloyd, 2026-11-22.** Magnus: "shift+enter does not produce a newline in a codex session", in a terminal
tab of Cloyd. `TerminalOfASessionHost.TypeANewLineOnShiftEnter` types a line feed, Claude Code's Ctrl+J. Its brief
keeps the change to `TerminalOfASessionHost`, plus the least that hands it the tab's `AgentCli`. new-view's
refactoring of each session's state will move where the terminal is made, and builds on this once it's merged.

**Then:** review, merge, run the full specifications, deploy. Codex takes Shift+Enter itself (crossterm keeps the Shift;
`keymap.rs` binds it to `editor.insert_newline`). The stand-in for Codex doesn't yet: gpt-collaborators' helper adds it,
and the tooling owner publishes it. Until then the helper's specification is skipped. Tell the helper to unskip it
once gpt-collaborators says it's published.

## Waiting for Compze

### Compze.xUnitBDD: a class's scenario run once for all its facts, opted into

**Passed on 2026-11-20** to the Compze session Magnus opened for it, `xfact-reuse-state`, on his go: he chose not to
wait until he is off API credits. **Its finding, the same day:** not cheap, and unlikely soon, if at all. In xUnit v3
4.0.3, `[XF]`'s test case has no runner of its own, the class, method and case runners are fixed singletons, and a case
can't know it is its class's last, to dispose the instance. The routes: a custom `ITestFramework`, about seven runner
subclasses and an assembly attribute in every consumer; the scenario as a class fixture, which changes how
specifications are written; or nothing. **Waits for:** Magnus's choice, there. Until then Cloyd has its own,
`SpecificationOpeningWindows.Once`, built 2026-11-20. **If Compze publishes it:** upgrade Cloyd's specifications to it
(the `upgrading-compze-packages` skill), move the classes on `Once` to it, measure with
`Cloyd/run-the-specifications.ps1 -Runs 1 -ShowTimes`, and remove this entry.

**Only as an opt-in** (Magnus, 2026-11-20): "the individual specs are not actually forbidden from changing state. That
is, I assume, one of the reasons why xunit runs full setup for every test. So changing the safe default is definitely
not going to happen. If it changes in compze it will be some sort of opt in where you promise very sincerely that in
this particular class, none of the test methods change the state of the test class..." Making a class's facts depend on
each other instead, as one scenario that does several things in turn, is a last resort.

**The cost, measured 2026-11-20:** Cloyd's 196 specifications take 5 min 23 s, 323 s of it in facts. Most of that is
each class's constructor, its scenario, run again for every `[XF]` fact, as xUnit makes a class instance per fact. The
worst class, `When_the_session_hosts_of_a_closed_project_window_end+and_the_project_window_opens_again`, has 9 facts of
about 5.8 s each, for a scenario that takes 5.8 s once.
