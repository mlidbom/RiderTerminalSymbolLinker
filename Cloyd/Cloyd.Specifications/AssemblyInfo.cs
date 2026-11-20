using Cloyd.Specifications.Scenarios;
using Xunit;

// One collection, so one specification after another: every window is on the headless session's one UI thread, and every
// session in the sandbox's one hub, whose typing into shells takes turns.
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]

[assembly: AssemblyFixture(typeof(ScenariosLeftEndWithTheRun))]
