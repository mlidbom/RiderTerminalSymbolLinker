# Backlog: work deferred, and what waits to be passed on

What Magnus has deferred, and what waits for a time or an event before it goes on to another repository. Each entry
says what it is, why it waits, and what to do once it can go ahead. Remove an entry once it's done.

## Waiting for Compze

### Compze.xUnitBDD: a class's scenario run once for all its facts, opted into

**Passed on 2026-11-20** to the Compze session Magnus opened for it, `xfact-reuse-state`, on his go: he chose not to
wait until he is off API credits. **Waits for:** Compze to publish it. **Then:** upgrade Cloyd's specifications to it
(the `upgrading-compze-packages` skill), opt in each class whose facts only read its state, measure with
`Cloyd/time-the-specifications.ps1`, and remove this entry.

**Only as an opt-in** (Magnus, 2026-11-20): "the individual specs are not actually forbidden from changing state. That
is, I assume, one of the reasons why xunit runs full setup for every test. So changing the safe default is definitely
not going to happen. If it changes in compze it will be some sort of opt in where you promise very sincerely that in
this particular class, none of the test methods change the state of the test class..." Making a class's facts depend on
each other instead, as one scenario that does several things in turn, is a last resort.

**The cost, measured 2026-11-20:** Cloyd's 196 specifications took 8 min 25 s, 506 s of it in facts. Most of that is
each class's constructor, its scenario, run again for every `[XF]` fact, as xUnit makes a class instance per fact. The
worst class, `When_the_session_hosts_of_a_closed_project_window_end+and_the_project_window_opens_again`, has 9 facts of
about 8.7 s each, for a scenario that takes 8.7 s once. Run once per class, the facts would take about 210 s.
