using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace RiderTerminalSymbolLinker.DevelopmentTools.Riders;

/// <summary>
///   The local API of a Claude tooling hub, ClaudeTooling's resident server, as the announcement it writes in
///   <paramref name="announcementFolder"/>, <c>local-api.json</c>, names it: the port it listens on, its token, and its
///   process. The contract is <c>~/.claude/dev_docs/the-hubs-local-api.md</c>.
/// </summary>
sealed class HubsLocalApi(string announcementFolder)
{
   /// <summary>The published hub's, which serves Magnus's Claude Code folder and IDEs: it announces in <c>%LOCALAPPDATA%\ClaudeTooling</c>.</summary>
   public static HubsLocalApi OfThePublishedHub =>
      new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeTooling"));

   public string AnnouncementFile => Path.Combine(announcementFolder, "local-api.json");

   /// <summary>What the hub's announcement says, or null while it has none.</summary>
   public JsonNode? Announcement() => File.Exists(AnnouncementFile) ? JsonNode.Parse(File.ReadAllText(AnnouncementFile)) : null;

   /// <summary>The process of the hub the announcement names, if it runs.</summary>
   public int? RunningProcessId() => Announcement()?["processId"]!.GetValue<int>() is {} processId && IsRunning(processId) ? processId : null;

   /// <summary>A client of the hub's local API, with its token, or null when no hub runs.</summary>
   public HttpClient? Client()
   {
      if(Announcement() is not {} announcement || !IsRunning(announcement["processId"]!.GetValue<int>())) return null;
      var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{announcement["port"]!.GetValue<int>()}"), Timeout = TimeSpan.FromSeconds(30) };
      client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", announcement["token"]!.GetValue<string>());
      return client;
   }

   /// <summary>
   ///   Asks the hub to restart <paramref name="ides"/>, by the names it knows them by (<see cref="EverydayIde.NameAtTheHub"/>),
   ///   through <c>POST /ide-restart</c>, as Magnus asks himself: he's shown no prompt. Each IDE's restarter runs
   ///   <paramref name="beforeItStartsAgain"/>, a program and its arguments, unless null, once the IDE has exited, before
   ///   it starts it again. Returns once the hub has started the restart, or joined one under way;
   ///   <see cref="TheIdeRestart"/> tells how it goes. The contract is its "Restarting the IDEs".
   /// </summary>
   public void AskToRestartTheIdes(string why, string askedBy, IReadOnlyList<string> ides, IReadOnlyList<string>? beforeItStartsAgain)
   {
      using var client = Client() ?? throw new InvalidOperationException($"No hub runs: none announces itself in {AnnouncementFile}.");
      var request = new JsonObject { ["why"] = why, ["askedBy"] = askedBy, ["askedByMagnus"] = true, ["ides"] = new JsonArray([.. ides.Select(it => JsonValue.Create(it))]) };
      if(beforeItStartsAgain is not null) request["beforeItStartsAgain"] = new JsonArray([.. beforeItStartsAgain.Select(it => JsonValue.Create(it))]);
      using var answer = client.PostAsync("/ide-restart", JsonContent.Create(request)).Result;
      if(answer.StatusCode != HttpStatusCode.Accepted)
         throw new InvalidOperationException($"The hub answered the request to restart {string.Join(", ", ides)} with {(int)answer.StatusCode}: {answer.Content.ReadAsStringAsync().Result}");
   }

   /// <summary>
   ///   The restart of the IDEs under way, else the last one since the hub started, else null, as the hub's
   ///   <c>GET /ide-restart</c> tells of it: its <c>stage</c>, <c>done</c> at the end, the <c>progress</c> of each step,
   ///   and its <c>outcome</c>.
   /// </summary>
   public JsonNode? TheIdeRestart()
   {
      using var client = Client() ?? throw new InvalidOperationException($"No hub runs: none announces itself in {AnnouncementFile}.");
      return JsonNode.Parse(client.GetStringAsync("/ide-restart").Result)!["restart"];
   }

   static bool IsRunning(int processId) => Process.GetProcesses().Any(it => it.Id == processId);
}
