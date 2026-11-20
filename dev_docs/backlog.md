# Backlog: work deferred, and what waits to be passed on

What Magnus has deferred, and what waits for a time or an event before it goes on to another repository. Each entry
says what it is, why it waits, and what to do once it can go ahead. Remove an entry once it's done.

## To pass on to Compze

### Compze.xUnitBDD: run a specification class's scenario once, for all its facts

**Waits for:** Magnus no longer running on API credits (Magnus, 2026-11-20: "get it passed on to Compze ASAP when we
are no longer running on API credits"). **Then, at once:** send it to the session that works in Compze
(`C:\Dev\Compze`, whose rep session is `compze-rep`; `ListAgents` names the sessions that run), or tell Magnus
when none runs. Once Compze publishes it, upgrade Cloyd's specifications to it (the `upgrading-compze-packages` skill)
and remove this entry.

**The cost, measured 2026-11-20:** Cloyd's 170 specifications take 3 min 10 s. Summed per class from the trx logger's
results, 200 s are spent in facts. Most of that is each class's constructor, its scenario, run again for every `[XF]`
fact, as xUnit makes a class instance per fact. The worst class, `When_the_session_hosts_of_a_closed_project_window_end`
`+and_the_project_window_opens_again`, has 9 facts of 5.6 s each, 50 s, for a scenario that takes 5.6 s once. Run once
per class, the facts would take about 73 s instead of 200 s.

**The recommendation:** Compze.xUnitBDD runs a class's scenario once, and its facts all read the same instance, which is
disposed after the last of them. xUnit v3 allows it: a test case runner whose `TestRunner.CreateTestClassInstance`
hands out one instance per class, and whose `DisposeTestClassInstance` disposes it after the class's last test. Every
consumer of Compze.xUnitBDD gains it. JAStudio's and Deskmancer's slow scenarios use `IClassFixture` for the same
reason, which doesn't fit the nested BDD classes: a nested class's scenario builds on its outer class's.

**Meanwhile, nothing is changed in Cloyd:** restructuring every specification into `IClassFixture` scenarios would undo
the nested-class style.
