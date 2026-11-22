using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Cloyd.History;
using Cloyd.Projects;
using Cloyd.ProjectsDashboard;
using Cloyd.Sessions;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

public class When_Magnus_opens_Projects_Dashboard : SpecificationOpeningWindows
{
   readonly SandboxProject _project;
   readonly ProjectWindow _projectWindow;
   readonly ProjectsDashboardWindow _dashboard;
   readonly SessionTab _tab;

   public When_Magnus_opens_Projects_Dashboard()
   {
      _project = Sandbox.Project("Project with a window");
      _projectWindow = Open(_project.Window);
      WaitUntil(() => _projectWindow.BringingBackTheTabs.IsCompleted, "the project window restored its tabs");
      DoneOnTheUIThread(() => _projectWindow.StartANewSession(new NewSession(AgentCli.Codex, "Codex in both windows")));
      _tab = OnTheUIThread(() => _projectWindow.SelectedTab!);
      WaitUntil(() => _tab.CodexChat?.InputBox.CanSend == true && _tab.Terminal.TextShown.Length > 0, "Codex was ready");
      Click(OnTheUIThread(() => _projectWindow.GetVisualDescendants().OfType<Button>().Single(it => Equals(it.Content, "Projects Dashboard"))));
      _dashboard = OnTheUIThread(() => Sandbox.WindowsOfCloyd.Dashboard!);
      WaitUntil(() => _dashboard.View?.Session.Running is not null, "Projects Dashboard selected the project's session",
                () => $"{_dashboard.Tree.Sessions.Count()} sessions, selected {_dashboard.View?.Host.Id}");
   }

   [XF] public void a_host_with_no_open_project_window_is_listed_without_attaching_to_it()
   {
      var other = Sandbox.Project("Closed project");
      var host = other.StartASessionHost(other.LabelOfCloydsHosts);
      WaitUntil(() => _dashboard.Tree.Sessions.Any(it => it.Host?.Id == host.Id && it.Group.Project!.Equals(other.Project)), "the shell appeared under its project");
      other.SessionHost(host.Id)!.Attached.Must().BeFalse();
      OnTheUIThread(() => Sandbox.WindowsOfCloyd.ShowDashboard(_project.Project)).Must().ReferenceEqual(_dashboard);
      Render(_dashboard, "Projects-Dashboard");
   }

   [XF] public void the_terminal_and_its_unsubmitted_input_move_between_windows_on_request()
   {
      SwitchWithCtrlH(_projectWindow, _tab);
      Type(_projectWindow, "before");
      WaitUntil(() => _tab.Terminal.TextShown.Contains("before"), "Codex showed the unfinished prompt");
      var connection = OnTheUIThread(() => _tab.Terminal.SessionId);
      Click(TheButtonWithTheTip(OnTheUIThread(() => _dashboard.View!), SessionView.TipOfTheTerminalsSwitch));
      WaitUntil(() => _dashboard.View is { ShowsTheChat: false } && _tab.ShowsTheChat && TopLevel.GetTopLevel(_tab.Terminal) == _dashboard,
                "the same terminal moved to the dashboard and the tab showed Chat",
                () => $"dashboard shows chat {_dashboard.View?.ShowsTheChat}, project shows chat {_tab.ShowsTheChat}, terminal in {(TopLevel.GetTopLevel(_tab.Terminal) as Window)?.Title}");
      Type(_dashboard, " and after");
      WaitUntil(() => _tab.Terminal.TextShown.Contains("before and after"), "the prompt survived the move");
      OnTheUIThread(() => _tab.Terminal.SessionId).Must().Be(connection);
      OnTheUIThread(_tab.FocusWhatItShows);
      Press(_projectWindow, Key.H, PhysicalKey.H, RawInputModifiers.Control);
      WaitUntil(() => TopLevel.GetTopLevel(_tab.Terminal) == _projectWindow && _dashboard.View!.ShowsTheChat, "the terminal returned to the project window");
   }

   [XF] public void an_unhosted_session_shows_its_conversation_read_only()
   {
      using var outside = ClaudeCodeInATerminalOfItsOwn.Running(Sandbox.HubOfTheSandbox, _project.Project.Folder, "Outside Cloyd", "Read this conversation");
      WaitUntil(() => _dashboard.Tree.Sessions.Any(it => it.Name == "Outside Cloyd" && !it.HasHost), "the running session appeared outside the hosted projects");
      OnTheUIThread(() => _dashboard.Select(_dashboard.Tree.Sessions.Single(it => it.Name == "Outside Cloyd")));
      WaitUntil(() => _dashboard.GetVisualDescendants().OfType<Cloyd.Chat.ChatOfAClaudeCodeSession>().Any(it =>
                   !it.InputBox.IsVisible && it.Shown.OfType<Cloyd.Chat.ShownInTheChat.Prompt>().Any(prompt => prompt.Text.Contains("Read this conversation"))),
                "the dashboard showed the external conversation without an editor");
   }

   [XF] public void reopening_the_dashboard_restores_the_host_selected_in_another_project()
   {
      var other = Sandbox.Project("Another project");
      var host = other.StartASessionHost(other.LabelOfCloydsHosts);
      WaitUntil(() => _dashboard.Tree.Sessions.Any(it => it.Host?.Id == host.Id), "the second project's shell appeared");
      OnTheUIThread(() => _dashboard.Select(_dashboard.Tree.Sessions.Single(it => it.Host?.Id == host.Id)));
      Close(_dashboard);
      var reopened = OnTheUIThread(() => Sandbox.WindowsOfCloyd.ShowDashboard(_project.Project));
      WaitUntil(() => reopened.View?.Host.Id == host.Id, "the dashboard restored its own selection");
   }

   [XF] public void starting_a_session_in_the_dashboard_adds_its_project_tab_without_switching_to_it()
   {
      DoneOnTheUIThread(() => _dashboard.Start(_project.Project, new NewSession(AgentCli.ClaudeCode, "New from dashboard")));
      WaitUntil(() => _projectWindow.Tabs.Any(it => it.Title == "New from dashboard"), "the project tab appeared");
      OnTheUIThread(() => _projectWindow.SelectedTab).Must().Be(_tab);
      OnTheUIThread(() => _dashboard.View!.Session).Must().ReferenceEqual(OnTheUIThread(() => _projectWindow.Tabs.Single(it => it != _tab).Session));
   }

   [XF] public void a_session_waiting_for_approval_marks_its_project_even_when_collapsed()
   {
      var group = OnTheUIThread(() => _dashboard.Tree.Projects.Single(it => it.Project?.Equals(_project.Project) == true));
      OnTheUIThread(() => ((TreeViewItem)_dashboard.GetVisualDescendants().OfType<TreeView>().Single().ContainerFromItem(group)!).IsExpanded = false);
      OnTheUIThread(_tab.FocusWhatItShows);
      Type(_projectWindow, "Ask for approval");
      Press(_projectWindow, Key.Enter, PhysicalKey.Enter, RawInputModifiers.Control);
      WaitUntil(() => group.IsWaiting && group.Sessions.Single().IsWaiting, "the collapsed project and its Codex session reported waiting");
   }

   [XF] public void typing_and_sending_in_either_Codex_chat_edits_and_submits_one_draft()
   {
      WaitUntil(() => _dashboard.View?.CodexChat?.InputBox.CanSend == true, "both editors could send");
      OnTheUIThread(_tab.FocusWhatItShows);
      Type(_projectWindow, "From the project");
      WaitUntil(() => _dashboard.View!.CodexChat!.InputBox.Draft.Document.Text == "From the project", "typing appeared in Projects Dashboard");
      OnTheUIThread(() => _dashboard.View!.FocusWhatItShows());
      Press(_dashboard, Key.End, PhysicalKey.End, RawInputModifiers.Control);
      Type(_dashboard, " and dashboard");
      WaitUntil(() => _tab.CodexChat!.InputBox.Draft.Document.Text == "From the project and dashboard", "the project saw the dashboard edit");
      Press(_dashboard, Key.Enter, PhysicalKey.Enter, RawInputModifiers.Control);
      WaitUntil(() => _tab.CodexChat!.InputBox.Draft.IsEmpty && _tab.CodexChat.Shown.OfType<Cloyd.Codex.Chat.CodexReply>().Any(it => it.Markdown == "Heard: From the project and dashboard"),
                "the project saw the single submitted prompt answered");
   }

   [XF] public void the_Claude_Code_editors_share_pasted_parts_and_a_draft_survives_closing_both_windows()
   {
      DoneOnTheUIThread(() => _projectWindow.StartANewSession(new NewSession(AgentCli.ClaudeCode, "Shared Claude")));
      var tab = OnTheUIThread(() => _projectWindow.SelectedTab!);
      WaitUntil(() => _dashboard.Tree.Sessions.Any(it => it.Host?.Id == tab.Host.Id), "the tree listed Claude");
      OnTheUIThread(() => _dashboard.Select(_dashboard.Tree.Sessions.Single(it => it.Host?.Id == tab.Host.Id)));
      OnTheUIThread(() => tab.ClaudeCodeChat.InputBox.Draft.Paste("pasted into Claude", new AvaloniaEdit.Document.TextSegment()));
      OnTheUIThread(() => _dashboard.View!.ClaudeCodeChat.InputBox.Draft.Parts.Single()).Must().Be(new Cloyd.Chat.Writing.PartOfAMessage.PastedText("pasted into Claude"));
      Close(_projectWindow);
      OnTheUIThread(() => _dashboard.View!.ClaudeCodeChat.InputBox.Draft.Document.Insert(0, "draft: "));
      Close(_dashboard);
      var reopened = Open(_project.Window);
      WaitUntil(() => reopened.BringingBackTheTabs.IsCompleted && reopened.SelectedTab?.ClaudeCodeChat.InputBox.Draft.Document.Text == "draft: pasted into Claude",
                "the project restored the newer draft saved in Projects Dashboard");
   }

   [XF] public void history_and_new_sessions_work_with_the_project_window_closed()
   {
      Close(_projectWindow);
      DoneOnTheUIThread(() => _dashboard.Start(_project.Project, new NewSession(AgentCli.ClaudeCode, "Claude from dashboard")));
      WaitUntil(() => _dashboard.View?.Session.LatestSession?.Name == "Claude from dashboard", "the dashboard started and selected Claude");
      Press(_dashboard, Key.Tab, PhysicalKey.Tab, RawInputModifiers.Control);
      WaitUntil(() => _dashboard.View?.Host.Id == _tab.Host.Id, "Ctrl+Tab selected Codex in the tree");
      Press(_dashboard, Key.Y, PhysicalKey.Y, RawInputModifiers.Control | RawInputModifiers.Alt | RawInputModifiers.Shift);
      WaitUntil(() => _dashboard.HistoryPicker.IsOpen && _dashboard.HistoryPicker.Scopes.Count > 0, "the history picker opened");
      OnTheUIThread(() => _dashboard.HistoryPicker.Scope).Must().Be(SessionHistoryScope.AllProjects);
      Sandbox.WindowsOfCloyd.Open.Count.Must().Be(0);
   }

   [XF] public void ending_the_session_in_the_dashboard_removes_the_saved_tab_of_its_closed_project_window()
   {
      Close(_projectWindow);
      var end = OnTheUIThread(() => _dashboard.GetVisualDescendants().OfType<Button>()
         .Single(it => Equals(ToolTip.GetTip(it), "End the session") && it.DataContext is DashboardSession row && row.Host?.Id == _tab.Host.Id));
      Click(end);
      WaitUntil(() => _project.SavedWindow.Tabs.Count == 0 && _dashboard.Tree.Sessions.All(it => it.Host?.Id != _tab.Host.Id), "ending the session also removed its saved tab");
      var reopened = Open(_project.Window);
      WaitUntil(() => reopened.BringingBackTheTabs.IsCompleted, "the project window reopened");
      OnTheUIThread(() => reopened.Tabs.Count).Must().Be(0);
   }

   public override void Dispose()
   {
      OnTheUIThread(() => Sandbox.WindowsOfCloyd.Dashboard?.Close());
      base.Dispose();
   }
}
