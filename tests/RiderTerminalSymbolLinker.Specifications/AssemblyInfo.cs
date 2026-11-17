using RiderTerminalSymbolLinker.Specifications;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

// Every specification asks the one sandbox Rider, set up once for the run.
[assembly: AssemblyFixture(typeof(TheSandboxWithTheFixtureSolution))]

// One click at a time: a click's outcome is the next one the plugin reports.
[assembly: Parallelization(Mode = ParallelMode.None)]
