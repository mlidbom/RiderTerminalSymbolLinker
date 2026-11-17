using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   A plugin's development observation channel: an HTTP server inside the <see cref="SandboxRider"/> that tells what
///   the plugin does, and drives it. Each of our plugins has its own, on a port of its own that the sandbox's
///   <c>idea.properties</c> gives it, and listens only there.
/// </summary>
/// <remarks>
///   Every call takes the project to ask about by name, or none when the sandbox has one open. Its answers are the
///   channel's JSON, as it sends them. Every channel's <c>/state</c> has <c>startedAt</c>, when the build of the plugin
///   that answers started, which tells when a new build has been reloaded.
/// </remarks>
public abstract class ObservationChannel
{
   static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(60) };
   static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

   readonly int _port;

   protected ObservationChannel(int port) => _port = port;

   /// <summary>What the plugin does now, as its channel's <c>/state</c> tells it.</summary>
   public JsonNode State() => Get("state");

   /// <summary>
   ///   Has the IDE look at its plugins folder again, so that it reloads each plugin whose files changed. Nothing else
   ///   makes it look there, but activating its window.
   /// </summary>
   public void RefreshThePluginsFolder() => Post("plugins/refresh", null, new { });

   /// <summary>
   ///   Waits until a build of the plugin that started after <paramref name="startedBefore"/> answers, as after a reload,
   ///   and returns how long that took.
   /// </summary>
   public TimeSpan WaitUntilReloaded(long? startedBefore, TimeSpan timeout)
   {
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      while(StartedAt() is not {} startedAt || startedAt == startedBefore)
      {
         if(stopwatch.Elapsed > timeout)
            throw new TimeoutException($"The plugin wasn't reloaded within {timeout}: its observation channel on port {_port} still answers from the build it had, or not at all. See the sandbox's idea.log.");
         Thread.Sleep(250);
      }

      return stopwatch.Elapsed;
   }

   /// <summary>When the build of the plugin that answers started, or null while none answers, as during a reload.</summary>
   public long? StartedAt()
   {
      try
      {
         return State()["startedAt"]!.GetValue<long>();
      }
      catch(HttpRequestException exception) when(exception.HttpRequestError == HttpRequestError.ConnectionError)
      {
         return null;
      }
   }

   public static string Indent(JsonNode answer) => answer.ToJsonString(Indented);

   protected JsonNode Get(string endpoint, string? project = null, string? query = null) =>
      Parse(Client.GetAsync(Address(endpoint, project, query)).GetAwaiter().GetResult());

   protected JsonNode Post(string endpoint, string? project, object body, string? query = null) =>
      Parse(Client.PostAsync(Address(endpoint, project, query), JsonContent.Create(body)).GetAwaiter().GetResult());

   string Address(string endpoint, string? project, string? query)
   {
      var parameters = new[] { project is null ? null : "project=" + Uri.EscapeDataString(project), query }.Where(it => it is not null);
      return $"http://127.0.0.1:{_port}/{endpoint}?{string.Join('&', parameters)}";
   }

   /// <summary>The channel's answer, which for a failure inside the plugin holds the failure, so a 500 is answered too.</summary>
   static JsonNode Parse(HttpResponseMessage response) =>
      JsonNode.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult())
   ?? throw new InvalidOperationException($"The observation channel answered {(int)response.StatusCode} with no JSON.");
}
