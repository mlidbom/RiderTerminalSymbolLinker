using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CloydSpike.Hub;

/// <summary>
/// The Claude tooling's hub, through its local API (<c>~/.claude/dev_docs/the-hubs-local-api.md</c>): loopback HTTP and
/// JSON with a token, found through the announcement it writes.
/// </summary>
/// <remarks>
/// The spike's client re-reads the announcement once when a request fails, as after a publish, and does nothing more for a
/// hub that changes under it.
/// </remarks>
sealed class TheHub
{
   static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

   HttpClient _http;

   TheHub(HttpClient http) => _http = http;

   static string Announcement => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeTooling", "local-api.json");

   /// <summary>The hub the announcement names now: an exception when none runs.</summary>
   public static TheHub Announced() => new(ClientOfTheHubAnnounced());

   static HttpClient ClientOfTheHubAnnounced()
   {
      var announcement = JsonNode.Parse(File.ReadAllText(Announcement))!;
      var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{(int)announcement["port"]!}/"), Timeout = Timeout.InfiniteTimeSpan };
      http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (string)announcement["token"]!);
      return http;
   }

   public async Task<IReadOnlyList<RunningSession>> RunningSessions() =>
      (await GetJson("running-sessions"))["runningSessions"]!.Deserialize<List<RunningSession>>(Json)!;

   public async Task<IReadOnlyList<SessionHostOfTheHub>> SessionHosts() =>
      (await GetJson("session-hosts"))["sessionHosts"]!.Deserialize<List<SessionHostOfTheHub>>(Json)!;

   /// <summary>Starts a session host running <paramref name="shell"/> in <paramref name="directory"/>, labelled as the spike's.</summary>
   public async Task<SessionHostOfTheHub> StartASessionHost(string shell, DirectoryInfo directory, IReadOnlyDictionary<string, string> environment)
   {
      var answer = await Post("session-hosts", new { shell, directory = directory.FullName, environment, label = SessionHostOfTheHub.LabelOfTheSpikes });
      return (await answer.Content.ReadFromJsonAsync<JsonNode>())!["sessionHost"]!.Deserialize<SessionHostOfTheHub>(Json)!;
   }

   public async Task End(SessionHostOfTheHub host) => await Post($"session-hosts/{host.Id}/end", new { });

   /// <summary>Types <c>claude --name <paramref name="name"/></c> into the shell of <paramref name="host"/>.</summary>
   public async Task StartClaudeCodeIn(SessionHostOfTheHub host, string name) => await Post($"shells/{host.ShellProcessId}/start-claude-code", new { name });

   /// <summary>Sends <paramref name="text"/> as Magnus's message: null once sent, else why it wasn't, as the hub says.</summary>
   public async Task<string?> Send(RunningSession session, string text)
   {
      var part = text.Any(it => char.IsControl(it) && it != '\n') ? new { kind = "pastedText", text } : new { kind = "typedText", text };
      using var answer = await SendRequest(() => new HttpRequestMessage(HttpMethod.Post, $"running-sessions/{session.ProcessId}/send") { Content = JsonContent.Create(new { parts = new[] { part } }) });
      if(answer.IsSuccessStatusCode) return null;
      var body = await answer.Content.ReadAsStringAsync();
      return $"{(int)answer.StatusCode}: {(JsonNode.Parse(body)?["error"]?.GetValue<string>() ?? body)}";
   }

   /// <summary>The conversation of the line of <paramref name="sessionId"/>, as the hub's stream sends it: all of it, then each change.</summary>
   public async IAsyncEnumerable<EventOfAConversation> ConversationOf(string sessionId, [EnumeratorCancellation] CancellationToken cancellation)
   {
      using var answer = await SendRequest(() => new HttpRequestMessage(HttpMethod.Get, $"sessions/{sessionId}/conversation/stream"), HttpCompletionOption.ResponseHeadersRead);
      answer.EnsureSuccessStatusCode();
      using var events = new StreamReader(await answer.Content.ReadAsStreamAsync(cancellation));
      await foreach(var conversationEvent in EventOfAConversation.ReadFrom(events, cancellation)) yield return conversationEvent;
   }

   async Task<JsonNode> GetJson(string path)
   {
      using var answer = await SendRequest(() => new HttpRequestMessage(HttpMethod.Get, path));
      answer.EnsureSuccessStatusCode();
      return (await answer.Content.ReadFromJsonAsync<JsonNode>())!;
   }

   async Task<HttpResponseMessage> Post(string path, object body)
   {
      var answer = await SendRequest(() => new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) });
      if(!answer.IsSuccessStatusCode) throw new HttpRequestException($"POST {path}: {(int)answer.StatusCode} {await answer.Content.ReadAsStringAsync()}");
      return answer;
   }

   async Task<HttpResponseMessage> SendRequest(Func<HttpRequestMessage> request, HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead)
   {
      try
      {
         var answer = await _http.SendAsync(request(), completion);
         if(answer.StatusCode != System.Net.HttpStatusCode.Unauthorized) return answer;
      }
      catch(HttpRequestException) { }

      _http = ClientOfTheHubAnnounced();
      return await _http.SendAsync(request(), completion);
   }
}
