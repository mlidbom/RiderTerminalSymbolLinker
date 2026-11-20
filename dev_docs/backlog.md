# Backlog: work deferred, and what waits to be passed on

What Magnus has deferred, and what waits for a time or an event before it goes on to another repository. Each entry
says what it is, why it waits, and what to do once it can go ahead. Remove an entry once it's done.

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
`Cloyd/time-the-specifications.ps1`, and remove this entry.

**Only as an opt-in** (Magnus, 2026-11-20): "the individual specs are not actually forbidden from changing state. That
is, I assume, one of the reasons why xunit runs full setup for every test. So changing the safe default is definitely
not going to happen. If it changes in compze it will be some sort of opt in where you promise very sincerely that in
this particular class, none of the test methods change the state of the test class..." Making a class's facts depend on
each other instead, as one scenario that does several things in turn, is a last resort.

**The cost, measured 2026-11-20:** Cloyd's 196 specifications take 5 min 23 s, 323 s of it in facts. Most of that is
each class's constructor, its scenario, run again for every `[XF]` fact, as xUnit makes a class instance per fact. The
worst class, `When_the_session_hosts_of_a_closed_project_window_end+and_the_project_window_opens_again`, has 9 facts of
about 5.8 s each, for a scenario that takes 5.8 s once.
