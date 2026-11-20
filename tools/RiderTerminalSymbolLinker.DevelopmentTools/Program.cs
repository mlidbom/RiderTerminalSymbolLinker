using RiderTerminalSymbolLinker.DevelopmentTools.Plugin;
using RiderTerminalSymbolLinker.DevelopmentTools.Riders;

namespace RiderTerminalSymbolLinker.DevelopmentTools;

/// <summary>The command line of the plugin's development tools: the <see cref="SandboxRider"/> that builds are verified in.</summary>
static class Program
{
   const string Usage = """
                        Usage: dotnet run --project tools/RiderTerminalSymbolLinker.DevelopmentTools -- <command>

                          sandbox start [<solution>]  Starts the sandbox Rider with a fresh copy of the fixture solution, or
                                                      the solution given, and waits until the MCP server on port 23861
                                                      answers with it indexed. Installs the plugin first if it never was.
                          sandbox reload RiderTerminalSymbolLinker|ClaudeSessions
                                                      Has the running sandbox reload the plugin as it is, as it does a new
                                                      build, and waits until it has.
                          sandbox install [--mcp marketplace]
                                                      Builds the working tree's plugin and puts it in the sandbox's plugins
                                                      folder, which a running sandbox reloads it from in a second. With it
                                                      the .NET Analysis&Refactoring MCP: by default the build the everyday
                                                      Riders run, its plugin and its deployed tools builds; or the
                                                      Marketplace's. A running sandbox restarts when the MCP's plugin
                                                      changes, and swaps to new tools builds at its next call.
                          sandbox stop                Closes the sandbox Rider, and ends the sessions its Claude Sessions
                                                      ran, which run on in their session hosts outside it
                          sandbox place               Puts the sandbox's windows in their place again
                          sandbox status              Whether it runs, where its windows are, what the MCP server answers

                          start, install and stop take the machine's sandbox turn, %LOCALAPPDATA%\SandboxRiders\turn.lock,
                          as the specifications do: they wait while another suite of sandbox Riders runs, such as the
                          .NET Analysis&Refactoring MCP's.

                        What the symbol linker does in the sandbox, through its development observation channel on port 23871:

                          sandbox state               Each open project's symbol and file indexes: ready, and their sizes
                          sandbox links <line>        The links the plugin makes in a line of terminal output
                          sandbox click <line> <link> Clicks the link with the text <link> in the line, and tells what it
                                                      did: opened a file and where the editor is, showed a picker and its
                                                      rows, found nothing, or fell back to Search Everywhere
                          sandbox picker choose <row> Chooses a row of the picker the last click showed, as Enter does
                          sandbox picker close        Closes that picker

                        What Claude Sessions does in the sandbox, through its development observation channel on port 23872:

                          sandbox sessions state      Each open project's Claude Sessions view: its tabs, the process and the
                                                      Claude Code session each runs, and the sessions it has saved
                          sandbox sessions open       Gives the view its content, as showing the tool window does
                          sandbox sessions new        Starts a new session in a tab, as the New Claude Session action does
                          sandbox sessions screen <tab>
                                                      The text tab <tab>'s terminal shows, counting from 0
                          sandbox sessions type <tab> <text>
                                                      Types the text into tab <tab>'s terminal, counting from 0, and Enter
                          sandbox sessions close <tab>
                                                      Closes tab <tab>, as its close button does: its session ends
                          sandbox chat <tab>          Tab <tab>'s chat: its cards, its transcript's rows, its find bar,
                                                      its status line and its input box
                          sandbox chat give <tab> <conversation.json>
                                                      Has the stand-in behind tab <tab>'s chat give it the conversation
                                                      in the file: {"items": [...]}, as the specifications give it
                          sandbox chat picture <tab>  Paints tab <tab>'s chat into a PNG file, and prints where it is

                        Magnus's IDEs:

                          everyday deploy             Builds the working tree's plugins and puts each in the plugins folder
                                                      of every installed IDE it runs in: the symbol linker in Rider, Claude
                                                      Sessions in Rider, IntelliJ IDEA and PyCharm. Says for each whether it
                                                      reloads it when he next switches to its window, or needs a restart
                          everyday deploy --dry-run   Builds the plugins and says what deploying them would do, changing
                                                      nothing in his IDEs: a line marked RESTART takes his go-ahead
                          everyday deploy --into-running-ides
                                                      Deploys as everyday deploy does, and also puts a build that needs a
                                                      restart into an IDE that runs and reloads, which breaks the plugin
                                                      in it until its restart: only when Magnus restarts it right after
                          everyday deploy --once-the-ides-close
                                                      Says what the deploy would do, and has it run once Magnus has closed
                                                      his IDEs, in a scheduled task that outlives them and the session
                                                      that asked: a message then tells him to start them again. For a
                                                      build that needs the IDEs closed, which an agent in one can't deploy
                          everyday reload RiderTerminalSymbolLinker|ClaudeSessions [<IDE>]
                                                      Has his IDEs, or only the one named, such as Rider or
                                                      "IntelliJ IDEA", reload the build of the plugin they have, when he
                                                      next switches to its window
                        """;

   static int Main(string[] arguments)
   {
      var sandbox = new SandboxRider(RiderInstallation.OfThisUser());

      switch(arguments)
      {
         case ["sandbox", "start"]:
            using(sandbox.TakeTheTurn("`sandbox start`")) sandbox.Start();
            break;
         case ["sandbox", "reload", var plugin]:
            sandbox.ReloadThePlugin(OurPlugin.Named(plugin));
            break;
         case ["sandbox", "start", var solution]:
            using(sandbox.TakeTheTurn("`sandbox start`")) sandbox.Start(Path.GetFullPath(solution));
            break;
         case ["sandbox", "install"]:
            using(sandbox.TakeTheTurn("`sandbox install`")) Console.WriteLine(sandbox.Install());
            break;
         case ["sandbox", "install", "--mcp", var mcp]:
            using(sandbox.TakeTheTurn("`sandbox install`")) Console.WriteLine(sandbox.Install(Enum.Parse<McpBuild>(mcp, ignoreCase: true)));
            break;
         case ["sandbox", "stop"]:
            using(sandbox.TakeTheTurn("`sandbox stop`")) Console.WriteLine($"Stopped the sandbox, and ended the {sandbox.StopAndEndItsSessions()} sessions its Claude Sessions ran.");
            break;
         case ["sandbox", "place"]:
            sandbox.PlaceWindows();
            break;
         case ["sandbox", "status"]:
            foreach(var line in sandbox.Status()) Console.WriteLine(line);
            break;
         case ["sandbox", "state"]:
            Console.WriteLine(ObservationChannel.Indent(sandbox.SymbolLinkerObservation.State()));
            break;
         case ["sandbox", "links", var line]:
            Console.WriteLine(ObservationChannel.Indent(sandbox.SymbolLinkerObservation.Links(line)));
            break;
         case ["sandbox", "click", var line, var link]:
            Console.WriteLine(ObservationChannel.Indent(sandbox.SymbolLinkerObservation.Click(line, link)));
            break;
         case ["sandbox", "picker", "choose", var row]:
            Console.WriteLine(ObservationChannel.Indent(sandbox.SymbolLinkerObservation.ChoosePickerRow(int.Parse(row))));
            break;
         case ["sandbox", "picker", "close"]:
            Console.WriteLine(ObservationChannel.Indent(sandbox.SymbolLinkerObservation.ClosePicker()));
            break;
         case ["sandbox", "sessions", "state"]:
            Console.WriteLine(ObservationChannel.Indent(sandbox.ClaudeSessionsObservation.State()));
            break;
         case ["sandbox", "sessions", "open"]:
            Console.WriteLine(ObservationChannel.Indent(sandbox.ClaudeSessionsObservation.OpenTheView()));
            break;
         case ["sandbox", "sessions", "new"]:
            Console.WriteLine(ObservationChannel.Indent(sandbox.ClaudeSessionsObservation.StartANewSession()));
            break;
         case ["sandbox", "sessions", "screen", var tab]:
            foreach(var line in sandbox.ClaudeSessionsObservation.Screen(int.Parse(tab))["lines"]!.AsArray()) Console.WriteLine(line!.GetValue<string>());
            break;
         case ["sandbox", "sessions", "type", var tab, var text]:
            Console.WriteLine(ObservationChannel.Indent(sandbox.ClaudeSessionsObservation.Type(int.Parse(tab), text)));
            break;
         case ["sandbox", "sessions", "close", var tab]:
            Console.WriteLine(ObservationChannel.Indent(sandbox.ClaudeSessionsObservation.Close(int.Parse(tab))));
            break;
         case ["sandbox", "chat", var tab]:
            Console.WriteLine(ObservationChannel.Indent(sandbox.ClaudeSessionsObservation.TheChat(int.Parse(tab))));
            break;
         case ["sandbox", "chat", "give", var tab, var conversationFile]:
            Console.WriteLine(ObservationChannel.Indent(sandbox.ClaudeSessionsObservation.GiveTheChatAConversation(int.Parse(tab), System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(conversationFile))!)));
            break;
         case ["sandbox", "chat", "picture", var tab]:
            Console.WriteLine(sandbox.ClaudeSessionsObservation.PictureOfTheChat(int.Parse(tab))["file"]!.GetValue<string>());
            break;
         case ["everyday", "reload", var plugin]:
            foreach(var line in EverydayIdes.Reload(OurPlugin.Named(plugin))) Console.WriteLine(line);
            break;
         case ["everyday", "reload", var plugin, var ide]:
            foreach(var line in EverydayIdes.Reload(OurPlugin.Named(plugin), EverydayIde.Named(ide))) Console.WriteLine(line);
            break;
         case ["everyday", "deploy"]:
            foreach(var line in EverydayIdes.Deploy(new GradleSandbox())) Console.WriteLine(line);
            break;
         case ["everyday", "deploy", "--dry-run"]:
            foreach(var line in EverydayIdes.Deploy(new GradleSandbox(), dryRun: true)) Console.WriteLine(line);
            break;
         case ["everyday", "deploy", "--into-running-ides"]:
            foreach(var line in EverydayIdes.Deploy(new GradleSandbox(), intoRunningIdes: true)) Console.WriteLine(line);
            break;
         case ["everyday", "deploy", "--once-the-ides-close"]:
            foreach(var line in DeployOnceTheIdesClose.SetUp()) Console.WriteLine(line);
            break;
         case ["everyday", "deploy", "--after-the-ides-close"]:
            DeployOnceTheIdesClose.WaitThenDeploy();
            break;
         default:
            Console.Error.WriteLine(Usage);
            return 1;
      }

      return 0;
   }
}
