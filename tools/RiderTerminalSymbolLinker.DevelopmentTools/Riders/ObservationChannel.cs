using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   The plugin's development observation channel, <c>dev.magnus.csl.observation.ObservationChannel</c>: an HTTP server
///   inside the <see cref="SandboxRider"/> that tells what the plugin does. It listens only there, on the port the
///   sandbox's <c>idea.properties</c> gives it.
/// </summary>
/// <remarks>
///   Every call takes the project to ask about by name, or none when the sandbox has one open. Its answers are the
///   channel's JSON, as it sends them.
/// </remarks>
public sealed class ObservationChannel
{
   static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(60) };
   static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

   readonly int _port;

   public ObservationChannel(int port) => _port = port;

   /// <summary>Each open project's symbol and file indexes: whether they're ready, and how much they hold.</summary>
   public JsonNode State() => Get("state");

   /// <summary>The links the plugin's filters make in <paramref name="line"/> of terminal output.</summary>
   public JsonNode Links(string line, string? project = null) => Post("links", project, new { line });

   /// <summary>
   ///   Clicks the link whose text is <paramref name="link"/> in <paramref name="line"/>, and tells what the click did:
   ///   opened a file, and where the editor is; showed a picker, and its rows; found nothing; or fell back to Search
   ///   Everywhere.
   /// </summary>
   public JsonNode Click(string line, string link, string? project = null) => Post("click", project, new { line, link });

   /// <summary>Chooses <paramref name="row"/> of the picker the last click showed, as Enter does, and tells what it did.</summary>
   public JsonNode ChoosePickerRow(int row, string? project = null) => Post("picker/choose", project, new { }, $"row={row}");

   /// <summary>Closes the picker the last click showed.</summary>
   public JsonNode ClosePicker() => Post("picker/close", null, new { });

   /// <summary>Closes every editor, so that a click opens its file afresh.</summary>
   public JsonNode CloseEveryEditor(string? project = null) => Post("editors/close-all", project, new { });

   /// <summary>
   ///   Has Rider look at its plugins folder again, so that it reloads the plugin when its files changed, and waits until
   ///   the reloaded plugin's channel answers. Returns how long the reload took.
   /// </summary>
   public TimeSpan ReloadThePlugin(TimeSpan timeout)
   {
      var before = StartedAt();
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      Post("plugins/refresh", null, new { });
      while(TryStartedAt() is not {} startedAt || startedAt == before)
      {
         if(stopwatch.Elapsed > timeout)
            throw new TimeoutException($"The plugin wasn't reloaded within {timeout}: its observation channel still answers from the build it had, or not at all. See the sandbox's idea.log.");
         Thread.Sleep(250);
      }

      return stopwatch.Elapsed;
   }

   long StartedAt() => State()["startedAt"]!.GetValue<long>();

   /// <summary>When the channel answering started, or null while none answers, as during a reload.</summary>
   long? TryStartedAt()
   {
      try
      {
         return StartedAt();
      }
      catch(HttpRequestException exception) when(exception.HttpRequestError == HttpRequestError.ConnectionError)
      {
         return null;
      }
   }

   public static string Indent(JsonNode answer) => answer.ToJsonString(Indented);

   JsonNode Get(string endpoint) => Parse(Client.GetAsync(Address(endpoint, null, null)).GetAwaiter().GetResult());

   JsonNode Post(string endpoint, string? project, object body, string? query = null) =>
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
