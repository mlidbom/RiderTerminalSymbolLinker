using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cloyd.DotNetAnalysisAndRefactoring;
using Cloyd.DotNetAnalysisAndRefactoring.Contract;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Cloyd.Specifications.TheSandbox;

/// <summary>
/// The .NET Analysis&amp;Refactoring MCP, as Cloyd sees it: Rider's, with the solutions that specifications have it
/// <see cref="Load"/>, each with its namespaces and types, and listing them as the real one does, through
/// <c>list_solutions</c> and <c>browse_namespace</c> in their JSON form. One for every specification, as Rider serves
/// every solution it has loaded, so that no window of a specification asks Magnus's Rider.
/// </summary>
sealed class StandInDotNetAnalysisAndRefactoringMcp
{
   /// <summary>The solutions loaded, by their solution files, each with its types by their namespaces: read by the server's threads.</summary>
   readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<TypeListing>>> _solutions = new(StringComparer.OrdinalIgnoreCase);

   public StandInDotNetAnalysisAndRefactoringMcp()
   {
      var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
      builder.WebHost.UseKestrelCore().ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));
      builder.Services.AddRoutingCore();
      var server = builder.Build();
      server.MapPost("/", async exchange => await exchange.Response.WriteAsJsonAsync(AnswerTo((await JsonNode.ParseAsync(exchange.Request.Body))!)));
      Task.Run(() => server.StartAsync()).GetAwaiter().GetResult();
      Mcp = new DotNetAnalysisAndRefactoringMcp(new Uri(server.Urls.Single() + "/"));
   }

   /// <summary>The MCP as a project window asks it.</summary>
   public DotNetAnalysisAndRefactoringMcp Mcp { get; }

   /// <summary>Has Rider load the solution <paramref name="solutionFile"/>, whose namespaces, by their qualified names, have <paramref name="types"/>.</summary>
   public void Load(string solutionFile, IReadOnlyDictionary<string, IReadOnlyList<TypeListing>> types) => _solutions[solutionFile] = types;

   /// <summary>Has Rider close the solution <paramref name="solutionFile"/>, as a specification is done with it.</summary>
   public void Close(string solutionFile) => _solutions.TryRemove(solutionFile, out _);

   /// <summary>The JSON-RPC answer to <paramref name="call"/>, a <c>tools/call</c>: the tool's answer, as JSON in the text of its one content.</summary>
   object AnswerTo(JsonNode call)
   {
      var arguments = call["params"]!["arguments"]!;
      object answer = call["params"]!["name"]!.GetValue<string>() switch
      {
         "list_solutions" => new SolutionsAnswer([.. _solutions.Keys.Select(it => new LoadedSolution(Path.GetFileNameWithoutExtension(it), it, null))]),
         "browse_namespace" => new NamespacesAnswer([.. arguments["namespaceNames"]!.AsArray().Select(it => Listing(SolutionNamed(arguments["solutionName"]!.GetValue<string>()), it!.GetValue<string>(), arguments["includeMembers"]!.GetValue<bool>()))]),
         var tool => throw new NotSupportedException($"The stand-in for Rider's MCP has no tool {tool}.")
      };
      return new { jsonrpc = "2.0", id = call["id"]!.GetValue<int>(), result = new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(answer, JsonSerializerOptions.Web) } } } };
   }

   IReadOnlyDictionary<string, IReadOnlyList<TypeListing>> SolutionNamed(string name) => _solutions.Single(it => Path.GetFileNameWithoutExtension(it.Key) == name).Value;

   /// <summary>The listing of <paramref name="namespace"/>: the namespaces directly in it, and its types, with their members if asked.</summary>
   static NamespaceListing Listing(IReadOnlyDictionary<string, IReadOnlyList<TypeListing>> solution, string @namespace, bool includingTheirMembers) =>
      new([.. AllNamespacesOf(solution).Where(it => it != @namespace && ParentOf(it) == @namespace)],
          [.. (solution.TryGetValue(@namespace, out var types) ? types : []).Select(it => includingTheirMembers ? it : it with { Members = null })]);

   /// <summary>Every namespace of <paramref name="solution"/>, the global one and the ones that hold its namespaces included.</summary>
   static IEnumerable<string> AllNamespacesOf(IReadOnlyDictionary<string, IReadOnlyList<TypeListing>> solution) =>
      solution.Keys.SelectMany(it => it.Split('.').Select((_, index) => string.Join('.', it.Split('.').Take(index + 1)))).Append("").Distinct();

   static string ParentOf(string @namespace) => @namespace.Contains('.') ? @namespace[..@namespace.LastIndexOf('.')] : "";
}
