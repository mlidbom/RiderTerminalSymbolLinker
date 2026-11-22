using System.Text.Json;
using System.Text.Json.Nodes;
using Cloyd.Projects;

namespace Cloyd.SessionHosts;

/// <summary>
/// The labels of the session hosts whose sessions a <see cref="ProjectWindow"/> shows: what the hub keeps, as it was
/// given, of whoever asked for a host, which tells the host's project.
/// </summary>
/// <remarks>
/// Two kinds of label name a project:
/// <list type="bullet">
/// <item>Cloyd's, which its project windows start their hosts with: <c>{"startedBy": "cloyd", "project": "&lt;folder&gt;"}</c>.</item>
/// <item>Claude Sessions', the JetBrains plugin whose tabs showed the sessions before Cloyd:
/// <c>{"ideConfigFolder": "&lt;the IDE's config folder&gt;", "project": "&lt;folder&gt;"}</c>. Its hosts are the project's
/// too, so that Cloyd takes over the sessions the plugin's tabs ran.</item>
/// </list>
/// The plugin takes only hosts whose label names an <c>ideConfigFolder</c>, so it never takes Cloyd's.
/// </remarks>
static class SessionHostLabel
{
   const string StartedBy = "startedBy", Cloyd = "cloyd", IdeConfigFolder = "ideConfigFolder", ProjectsFolder = "project";

   /// <summary>The label a project window starts the hosts of <paramref name="project"/>'s new sessions with.</summary>
   public static string OfANewHostOf(Project project) => new JsonObject { [StartedBy] = Cloyd, [ProjectsFolder] = project.Folder }.ToJsonString();

   /// <summary>The same project label, identifying a coordinator's one launch across a Cloyd restart.</summary>
   public static string ForAPeer(Project project, Guid requestId) =>
      new JsonObject { [StartedBy] = Cloyd, [ProjectsFolder] = project.Folder, ["peerRequest"] = requestId.ToString("D") }.ToJsonString();

   /// <summary>
   /// The project whose window shows the host labelled <paramref name="label"/>: null for a label neither Cloyd nor Claude
   /// Sessions gave, as another tool's, or none.
   /// </summary>
   public static Project? ProjectOf(string? label)
   {
      if(label is null || ObjectIn(label) is not {} json || json[ProjectsFolder] is not JsonValue project) return null;
      var isCloyds = json[StartedBy] is JsonValue startedBy && startedBy.GetValueKind() == JsonValueKind.String && (string)startedBy! == Cloyd;
      var isClaudeSessions = json[IdeConfigFolder] is JsonValue;
      return (isCloyds || isClaudeSessions) && project.GetValueKind() == JsonValueKind.String ? new Project((string)project!) : null;
   }

   /// <summary>The JSON object <paramref name="label"/> is: null for a label that is none, as another tool's may be.</summary>
   static JsonObject? ObjectIn(string label)
   {
      try
      {
         return JsonNode.Parse(label) as JsonObject;
      }
      catch(JsonException)
      {
         return null; // The hub keeps any label as it is given: another tool's may be no JSON at all.
      }
   }
}
