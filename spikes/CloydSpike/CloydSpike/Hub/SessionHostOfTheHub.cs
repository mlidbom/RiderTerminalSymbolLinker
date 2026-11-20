namespace CloydSpike.Hub;

/// <summary>A session host, as <c>GET /session-hosts</c> lists it.</summary>
sealed record SessionHostOfTheHub(string Id, int ProcessId, int ShellProcessId, string Shell, string Directory, string? Label, bool Attached, string[] AttachCommand)
{
   /// <summary>
   /// The label the spike starts its hosts with. Claude Sessions takes only hosts labelled with its own IDE and project, so
   /// no IDE's tab attaches to one, and the spike attaches to no other: attaching takes a session over from its tab.
   /// </summary>
   public const string LabelOfTheSpikes = """{"cloydSpike":"throwaway"}""";

   public bool IsTheSpikes => Label == LabelOfTheSpikes;

   public override string ToString() => $"{Id} · {Directory}{(Attached ? " · attached" : "")}";
}
