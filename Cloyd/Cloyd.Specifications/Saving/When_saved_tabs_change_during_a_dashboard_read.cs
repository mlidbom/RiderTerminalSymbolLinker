using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Chat.Writing;
using Cloyd.Codex;
using Cloyd.Projects;
using Cloyd.Saving;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Saving;

public class When_saved_tabs_change_during_a_dashboard_read : IDisposable
{
   readonly CloydsFolder _folder = new(Directory.CreateTempSubdirectory("cloyd-tab-revisions-"));
   readonly Project _project = new(@"C:\Dev\Project");
   readonly SavedSessionTab _tab = new(SessionHostId.New(), @"C:\Dev\Project", new CodexSession(new CodexSessionId("codex-session"), "Session"));

   [XF] public void changing_tabs_and_putting_them_back_still_invalidates_the_original_read()
   {
      var windows = _folder.SavedProjectWindows;
      var original = new SavedProjectWindow([_tab], _tab.SessionHost, null);
      windows.Save(_project, original);
      var revision = windows.RestorationRevision;
      windows.Save(_project, original with { SelectedTab = null, Tabs = [_tab with { Draft = [new PartOfAMessage.TypedText("draft")] }] });
      windows.RestorationRevision.Must().Be(revision);
      windows.Forget(_tab.SessionHost);
      windows.Save(_project, original);
      _folder.SavedProjectWindows.RestorationRevision.Must().Satisfy(it => it > revision);
      _folder.SavedProjectWindows.TabsOfClosedWindows([]).Single().SessionHost.Must().Be(_tab.SessionHost);
   }

   [XF] public void the_last_agent_working_folder_invalidates_a_read_while_draft_edits_do_not()
   {
      var sessions = _folder.SavedHostedSessions;
      sessions.Save(_tab);
      var revision = sessions.RestorationRevision;
      sessions.Save(_tab with { Draft = [new PartOfAMessage.TypedText("changed")] });
      sessions.RestorationRevision.Must().Be(revision);
      sessions.Save(_tab with { SessionDirectory = @"C:\Dev\SecondCheckout" });
      sessions.Save(_tab);
      _folder.SavedHostedSessions.RestorationRevision.Must().Satisfy(it => it > revision);
   }

   public void Dispose() => _folder.Folder.Delete(recursive: true);
}
