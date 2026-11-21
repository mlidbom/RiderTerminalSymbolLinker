# Backlog: work deferred, and what waits to be passed on

What Magnus has deferred, and what waits for a time or an event before it goes on to another repository. Each entry
says what it is, why it waits, and what to do once it can go ahead. Remove an entry once it's done.

## Deferred by Magnus

### Cloyd's chat doesn't show what an idle session waits on: its background shells and monitors

**Magnus, 2026-11-21:** "I have no way in the new UI to see that a session in waiting on something". His screenshots put
the TUI beside the chat:
- **The TUI** shows, after a turn, `✻ Sautéed for 21s · done 2:37 PM · 1 shell still running`, and under its box, after
  the permission mode, `· 1 shell · ↓ to manage`.
- **The chat** shows neither. Its status line has the machine's status line and the permission mode, nothing more.

**What Cloyd has to go on:**
- **The hub's `status` is `shell`** while Claude Code is idle with a background shell task or monitor running
  (`StatusOfAClaudeCode.AsReported`). Until 2026-11-21 the status line's left side then said "Idle · a background
  task runs". That went with the left side, on Magnus's word ("This is not needed with progress clearly shown above
  the input"), and nothing took its place.
- **How many tasks run, and of which kind,** shells or monitors, aren't in the hub's answer. Nor is the turn's end,
  "done 2:37 PM". The hub reads the permission mode off Claude Code's screen (`permissionMode.shownAs`), but not the
  rest of that line, `1 shell · ↓ to manage`.

**What to do once Magnus takes it up:**
1. Ask the tooling owner for the background tasks in each running session's record: how many, of which kind, and
   perhaps each one's command.
2. Show them as the TUI does: a line over the input box while any run, such as `1 shell still running`, a marker in
   the tab's header, and the turn's end line, such as `Worked for 21s · done 2:37 PM`.
3. Specify it with the stand-in for Claude Code, once it can run a background task.

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
