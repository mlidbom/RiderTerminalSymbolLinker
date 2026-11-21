using Cloyd.ClaudeCode;
using Cloyd.Projects;
using Cloyd.Saving;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Saving;

/// <summary>
/// A project window's file as a Cloyd saved it before it saved each tab's session with its agent CLI, read by
/// <see cref="SavedProjectWindows"/>: as Magnus's saved windows are, the first time a Cloyd that knows of other agent CLIs
/// than Claude Code opens them.
/// </summary>
public class When_a_project_window_saved_before_sessions_were_saved_with_their_agent_CLI_is_read : IDisposable
{
   const string AsAnEarlierCloydSavedIt = """
      {
        "tabs": [
          {
            "sessionHost": "be700cddf8f67965",
            "directory": "C:\\Dev\\TheProject",
            "session": {
              "id": "cf33d81b-6e8b-4c94-85cc-f9e2de9fce51",
              "name": "Fixing the login"
            }
          }
        ],
        "selectedTab": "be700cddf8f67965",
        "desktop": null
      }
      """;

   static readonly ClaudeCodeSession TheSavedSession = new(new ClaudeCodeSessionId("cf33d81b-6e8b-4c94-85cc-f9e2de9fce51"), "Fixing the login");

   readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("cloyd-specification-");
   readonly SavedProjectWindow _read;
   readonly string _savedAgain;
   readonly SavedProjectWindow _readAgain;

   public When_a_project_window_saved_before_sessions_were_saved_with_their_agent_CLI_is_read()
   {
      var windows = new SavedProjectWindows(_folder);
      var project = new Project(@"C:\Dev\TheProject");
      windows.Save(project, SavedProjectWindow.NothingSaved);
      var file = _folder.GetFiles().Single();
      File.WriteAllText(file.FullName, AsAnEarlierCloydSavedIt);
      _read = windows.Of(project);
      windows.Save(project, _read);
      _savedAgain = File.ReadAllText(file.FullName);
      _readAgain = windows.Of(project);
   }

   [XF] public void its_tabs_session_is_a_Claude_Code_session_with_its_id_and_name() => _read.Tabs.Single().Session.Must().Be(TheSavedSession);

   [XF] public void saved_again_its_session_is_saved_as_Claude_Codes() => _savedAgain.Must().Contain("\"agentCli\": \"claudeCode\"");

   [XF] public void saved_again_it_reads_the_same() => _readAgain.Tabs.Single().Session.Must().Be(TheSavedSession);

   public void Dispose() => _folder.Delete(recursive: true);
}
