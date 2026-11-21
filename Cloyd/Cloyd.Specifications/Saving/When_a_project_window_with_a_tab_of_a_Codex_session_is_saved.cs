using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Codex;
using Cloyd.Projects;
using Cloyd.Saving;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Saving;

/// <summary>
/// A project window with a tab whose session is Codex's, saved and read again by <see cref="SavedProjectWindows"/>: as
/// Cloyd saves a window whose tab Magnus started Codex in, and reads it when he next opens the project.
/// </summary>
public class When_a_project_window_with_a_tab_of_a_Codex_session_is_saved : IDisposable
{
   static readonly CodexSession TheSession = new(new CodexSessionId("01a1fe6b-c285-7962-a6ae-85f69a3a51f3"), "Porting the parser");

   readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("cloyd-specification-");
   readonly string _saved;
   readonly SavedProjectWindow _read;

   public When_a_project_window_with_a_tab_of_a_Codex_session_is_saved()
   {
      var windows = new SavedProjectWindows(_folder);
      var project = new Project(@"C:\Dev\TheProject");
      var host = new SessionHostId("be700cddf8f67965");
      windows.Save(project, new SavedProjectWindow([new SavedSessionTab(host, project.Folder, TheSession)], host, Desktop: null));
      _saved = File.ReadAllText(_folder.GetFiles().Single().FullName);
      _read = windows.Of(project);
   }

   [XF] public void its_session_is_saved_as_Codexs() => _saved.Must().Contain("\"agentCli\": \"codex\"");

   [XF] public void it_reads_as_a_Codex_session_with_its_id_and_name() => _read.Tabs.Single().Session.Must().Be(TheSession);

   public void Dispose() => _folder.Delete(recursive: true);
}
