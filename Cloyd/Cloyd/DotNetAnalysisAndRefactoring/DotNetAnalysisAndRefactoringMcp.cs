using System.Net.Sockets;
using System.Text.Json;
using Cloyd.DotNetAnalysisAndRefactoring.Contract;
using Cloyd.Links.Symbols;
using Cloyd.Projects;

namespace Cloyd.DotNetAnalysisAndRefactoring;

/// <summary>
/// The .NET Analysis&amp;Refactoring MCP, Royder: Magnus's fork of Joshua Light's ReSharper MCP, whose server runs inside
/// Rider's ReSharper backend and answers for every solution Rider has loaded. From it Cloyd learns the C# types and
/// members that a terminal's output can link to, as the symbol linker does in Rider's terminal.
/// </summary>
/// <remarks>
/// Plain JSON-RPC over HTTP on the loopback address: <c>POST /</c>, method <c>tools/call</c>. Each tool is asked for its
/// JSON form, <c>"format": "json"</c>, which is a contract where its text form is written for people
/// (<c>dev_docs/architecture.md</c>, "Where the symbols come from").<br/>
/// One call after another: the ReSharper backend serializes analysis, so calls in parallel gain little, and short calls,
/// one namespace each, keep other clients from waiting long behind Cloyd's. Compze's 609 namespaces, 5,221 types and
/// 12,320 members took 6.4 s, its slowest call 258 ms (2026-11-20).
/// </remarks>
sealed class DotNetAnalysisAndRefactoringMcp(Uri address)
{
   /// <summary>The port the MCP listens on, unless <see cref="VariableNamingThePort"/> names another.</summary>
   const int UsualPort = 23751;

   /// <summary>The variable that the MCP's server reads its port from, when it's set, as Rider passes its environment on to it.</summary>
   const string VariableNamingThePort = "DOTNET_ANALYSIS_AND_REFACTORING_MCP_PORT";

   /// <summary>How many namespaces one call asks for while the namespaces are found, before their types are listed one namespace to a call.</summary>
   const int NamespacesToACall = 50;

   /// <summary>The global namespace, as <c>browse_namespace</c> names it.</summary>
   const string TheGlobalNamespace = "";

   static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

   readonly Uri _address = address;

   /// <summary>The MCP of Magnus's Rider.</summary>
   public static DotNetAnalysisAndRefactoringMcp OnThisMachine() =>
      new(new Uri($"http://127.0.0.1:{(int.TryParse(Environment.GetEnvironmentVariable(VariableNamingThePort), out var port) ? port : UsualPort)}/"));

   /// <summary>
   /// The names of the types and members of each solution Rider has loaded in <paramref name="project"/>'s folder, or in
   /// a folder in it: none when it has loaded none there.
   /// </summary>
   /// <exception cref="NotAnsweredByTheDotNetAnalysisAndRefactoringMcp">It couldn't answer now.</exception>
   public async Task<SymbolNames> SymbolNamesOfTheSolutionsIn(Project project, CancellationToken cancellation)
   {
      var gatherer = new SymbolNamesGatherer();
      foreach(var solution in (await Answer<SolutionsAnswer>("list_solutions", new(), cancellation)).Solutions.Where(it => it.IsIn(project)))
         foreach(var @namespace in await NamespacesOf(solution, cancellation))
            foreach(var type in (await Listed([@namespace], includingTheirMembers: true, solution, cancellation)).Single().Types)
               gatherer.Add(type);
      return gatherer.Gathered();
   }

   /// <summary>The qualified names of every namespace of <paramref name="solution"/>, the global one first.</summary>
   async Task<IReadOnlyList<string>> NamespacesOf(LoadedSolution solution, CancellationToken cancellation)
   {
      var found = new List<string>();
      var notYetListed = new Queue<string>([TheGlobalNamespace]);
      while(notYetListed.Count > 0)
      {
         var batch = Enumerable.Range(0, Math.Min(NamespacesToACall, notYetListed.Count)).Select(_ => notYetListed.Dequeue()).ToList();
         found.AddRange(batch);
         foreach(var listing in await Listed(batch, includingTheirMembers: false, solution, cancellation))
            foreach(var child in listing.ChildNamespaces) notYetListed.Enqueue(child);
      }
      return found;
   }

   async Task<IReadOnlyList<NamespaceListing>> Listed(IReadOnlyList<string> namespaces, bool includingTheirMembers, LoadedSolution solution, CancellationToken cancellation) =>
      (await Answer<NamespacesAnswer>("browse_namespace",
                                      new() { ["namespaceNames"] = namespaces, ["includeMembers"] = includingTheirMembers, ["solutionName"] = solution.RoutingKey },
                                      cancellation)).Namespaces;

   /// <summary>What <paramref name="tool"/> answers, asked with <paramref name="arguments"/>, in its JSON form.</summary>
   /// <exception cref="NotAnsweredByTheDotNetAnalysisAndRefactoringMcp">It couldn't answer now.</exception>
   async Task<TAnswer> Answer<TAnswer>(string tool, Dictionary<string, object> arguments, CancellationToken cancellation)
   {
      arguments["format"] = "json";
      var call = new { jsonrpc = "2.0", id = 1, method = "tools/call", @params = new { name = tool, arguments } };
      HttpResponseMessage response;
      try
      {
         response = await Http.PostAsJsonAsync(_address, call, cancellation);
      }
      // Nothing listens on the port while Rider doesn't run.
      catch(HttpRequestException notThere) when(notThere.InnerException is SocketException)
      {
         throw new NotAnsweredByTheDotNetAnalysisAndRefactoringMcp($"Nothing answers at {_address}: Rider doesn't run, or runs no .NET Analysis&Refactoring MCP.");
      }
      using(response)
      {
         response.EnsureSuccessStatusCode();
         var answer = (await response.Content.ReadFromJsonAsync<JsonRpcAnswer>(JsonSerializerOptions.Web, cancellation))!;
         if(answer.Error is {} error) throw new NotAnsweredByTheDotNetAnalysisAndRefactoringMcp($"It refused {tool}: {error}");
         var text = answer.Result!.Content[0].Text;
         if(!text.TrimStart().StartsWith('{')) throw new NotAnsweredByTheDotNetAnalysisAndRefactoringMcp($"It answered {tool} in text, not in JSON: it's older than its JSON form, 0.15.0.");
         using var json = JsonDocument.Parse(text);
         if(json.RootElement.TryGetProperty("error", out var why)) throw new NotAnsweredByTheDotNetAnalysisAndRefactoringMcp($"It couldn't answer {tool}: {why}");
         return json.RootElement.Deserialize<TAnswer>(JsonSerializerOptions.Web)!;
      }
   }
}
