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
            sandbox.Install();
            Console.WriteLine(sandbox.IsRunning
                                 ? "Built the plugin into Gradle's sandbox: the running sandbox Rider reloads it by itself."
                                 : "Built the plugin into Gradle's sandbox: the sandbox Rider loads it when it starts.");
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
         default:
            Console.Error.WriteLine(Usage);
            return 1;
      }

      return 0;
   }
}
