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
                          sandbox install             Builds the working tree's plugin into Gradle's sandbox, which the
                                                      sandbox Rider loads it from; a running one reloads it by itself
                          sandbox stop                Closes the sandbox Rider
                          sandbox place               Puts the sandbox's windows in their place again
                          sandbox status              Whether it runs, where its windows are, what the MCP server answers

                        What the plugin does in the sandbox, through its development observation channel on port 23871:

                          sandbox state               Each open project's symbol and file indexes: ready, and their sizes
                          sandbox links <line>        The links the plugin makes in a line of terminal output
                          sandbox click <line> <link> Clicks the link with the text <link> in the line, and tells what it
                                                      did: opened a file and where the editor is, showed a picker and its
                                                      rows, found nothing, or fell back to Search Everywhere
                          sandbox picker choose <row> Chooses a row of the picker the last click showed, as Enter does
                          sandbox picker close        Closes that picker

                        Magnus's Riders:

                          everyday deploy             Builds the working tree's plugin and puts it in the everyday plugins
                                                      folder; each everyday Rider reloads it, without a restart, when he
                                                      next switches to its window
                        """;

   static int Main(string[] arguments)
   {
      var sandbox = new SandboxRider(RiderInstallation.OfThisUser());

      switch(arguments)
      {
         case ["sandbox", "start"]:
            sandbox.Start();
            break;
         case ["sandbox", "start", var solution]:
            sandbox.Start(Path.GetFullPath(solution));
            break;
         case ["sandbox", "install"]:
            Console.WriteLine(sandbox.Install());
            break;
         case ["sandbox", "stop"]:
            sandbox.Stop();
            break;
         case ["sandbox", "place"]:
            sandbox.PlaceWindows();
            break;
         case ["sandbox", "status"]:
            foreach(var line in sandbox.Status()) Console.WriteLine(line);
            break;
         case ["sandbox", "state"]:
            Console.WriteLine(ObservationChannel.Indent(sandbox.Observation.State()));
            break;
         case ["sandbox", "links", var line]:
            Console.WriteLine(ObservationChannel.Indent(sandbox.Observation.Links(line)));
            break;
         case ["sandbox", "click", var line, var link]:
            Console.WriteLine(ObservationChannel.Indent(sandbox.Observation.Click(line, link)));
            break;
         case ["sandbox", "picker", "choose", var row]:
            Console.WriteLine(ObservationChannel.Indent(sandbox.Observation.ChoosePickerRow(int.Parse(row))));
            break;
         case ["sandbox", "picker", "close"]:
            Console.WriteLine(ObservationChannel.Indent(sandbox.Observation.ClosePicker()));
            break;
         case ["everyday", "deploy"]:
            Console.WriteLine(new EverydayRider(RiderInstallation.OfThisUser()).Deploy());
            break;
         default:
            Console.Error.WriteLine(Usage);
            return 1;
      }

      return 0;
   }
}
