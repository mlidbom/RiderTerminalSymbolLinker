using Cloyd.Hub;
using Cloyd.Projects;
using Cloyd.Sessions;

namespace Cloyd.History;

/// <summary>Where the picker shows a chosen session. A project window can bring another project's tab forward.
/// Projects Dashboard selects that host in its own tree.</summary>
interface ISessionHistoryTarget
{
   Task ResumeInANewTab(Session session);
   void ShowInATab(SessionHostOfTheHub host, Session session);
   Task ChooseExistingTab(ProjectWindow owner, SessionTab tab, Session session, bool resume);
   Task ChooseDashboardHost(SessionHostOfTheHub host, Session session, bool resume);
}
