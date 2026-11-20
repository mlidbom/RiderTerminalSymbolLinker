using Cloyd.Specifications.Scenarios;
using Xunit;

// One collection, so one specification after another: every window is on the headless session's one UI thread, whose
// keyboard focus is one for the process. Each scenario has a sandbox of its own, its hub included: specifications run at
// once in runs of their own, each in a process of its own.
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]

[assembly: AssemblyFixture(typeof(ScenariosLeftEndWithTheRun))]
