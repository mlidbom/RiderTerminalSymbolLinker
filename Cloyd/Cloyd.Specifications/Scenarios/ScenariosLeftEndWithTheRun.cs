namespace Cloyd.Specifications.Scenarios;

/// <summary>
/// Ends, as the specifications' run ends, each <see cref="ScenarioOfAClass"/> that didn't end after its class's last fact,
/// such as one of a class a filter picked only some facts of. An assembly fixture of xUnit's, which disposes it as the run
/// ends: see <c>AssemblyInfo.cs</c>.
/// </summary>
public sealed class ScenariosLeftEndWithTheRun : IDisposable
{
   public void Dispose() => ScenarioOfAClass.EndThoseLeft();
}
