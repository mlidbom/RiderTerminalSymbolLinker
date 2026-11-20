using Cloyd.Specifications.Scenarios;
using Cloyd.Specifications.TheSandbox;

namespace Cloyd.Specifications;

/// <summary>
/// What ends as the specifications' run ends: each <see cref="ScenarioOfAClass"/> that didn't end after its class's last
/// fact, such as one of a class a filter picked only some facts of, and then the run's sandboxes, which end in the
/// background, waited for here (<see cref="SandboxesOfThisRun"/>). An assembly fixture of xUnit's, which disposes it as the
/// run ends: see <c>AssemblyInfo.cs</c>.
/// </summary>
public sealed class WhatEndsWithTheRun : IDisposable
{
   public void Dispose()
   {
      try
      {
         ScenarioOfAClass.EndThoseLeft();
      }
      finally
      {
         SandboxesOfThisRun.WaitUntilAllHaveEnded();
      }
   }
}
