using Cloyd.Hub;

namespace Cloyd.Sessions;

/// <summary>A new session, as Magnus asks for one with the <see cref="NewSessionButton"/>: of an agent CLI, named or not.</summary>
/// <param name="AgentCli">The agent CLI to run it.</param>
/// <param name="Name">The name Magnus gave it: null for none.</param>
sealed record NewSession(AgentCli AgentCli, string? Name)
{
   /// <summary>The command that starts it in a session host's shell.</summary>
   public AgentCliCommand Command => AgentCli.CommandOfANewSession(Name);

   /// <summary>The title of its tab until it runs: its name, else what its agent CLI titles a session with no name.</summary>
   public string Title => Name ?? AgentCli.TitleOfASessionWithNoName;
}
