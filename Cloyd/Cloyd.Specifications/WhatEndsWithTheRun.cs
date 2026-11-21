using Cloyd.Specifications.Scenarios;
using Cloyd.Specifications.TheSandbox;

namespace Cloyd.Specifications;

/// <summary>
/// What ends as the specifications' run ends: each <see cref="ScenarioOfAClass"/> that didn't end after its class's last
/// fact, such as one of a class a filter picked only some facts of, and then the run's sandboxes, which end in the
/// background, waited for here (<see cref="SandboxesOfThisRun"/>). An assembly fixture of xUnit's, which disposes it as the
/// run ends: see <c>AssemblyInfo.cs</c>.
/// </summary>
/// <remarks>
/// xUnit tells of a failure here only its type, <c>Xunit.Sdk.TestPipelineException</c>, in neither the console nor the
/// trx: so the failure is also written whole to the file <see cref="VariableNamingTheFileOfAFailure"/> names, where
/// <c>run-the-specifications.ps1</c> reads it.
/// </remarks>
public sealed class WhatEndsWithTheRun : IDisposable
{
   /// <summary>The variable that names the file a failure as the run ends is written to: none is written where it names none.</summary>
   public const string VariableNamingTheFileOfAFailure = "CLOYD_SPECIFICATIONS_FILE_OF_A_FAILURE_AS_THE_RUN_ENDS";

   public void Dispose()
   {
      try
      {
         EndWhatIsLeft();
      }
      catch(Exception failure)
      {
         if(Environment.GetEnvironmentVariable(VariableNamingTheFileOfAFailure) is { Length: > 0 } file) File.WriteAllText(file, failure.ToString());
         throw;
      }
   }

   static void EndWhatIsLeft()
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
