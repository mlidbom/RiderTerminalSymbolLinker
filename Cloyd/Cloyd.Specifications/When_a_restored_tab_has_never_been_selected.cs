using ClaudeTooling.SessionHost.Protocol;
using Cloyd.Projects;
using Cloyd.Saving;
using Cloyd.SessionHosts;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

public class When_a_restored_tab_has_never_been_selected : SpecificationOpeningWindows
{
   readonly SandboxProject _project;
   readonly ProjectWindow _window;
   readonly SessionHostId _hidden;

   public When_a_restored_tab_has_never_been_selected()
   {
      _project = Sandbox.Project("Restored shells");
      var selected = _project.StartASessionHost(_project.LabelOfCloydsHosts);
      var script = Path.Combine(_project.Project.Folder, "write-until-complete.ps1");
      File.WriteAllText(script, """
         while (!(Test-Path -LiteralPath start-writing)) { Start-Sleep -Milliseconds 50 }
         $line = '0123456789' * 100
         for ($i = 0; $i -lt 6000; $i++) { [Console]::WriteLine($line) }
         Set-Content -LiteralPath finished-writing -Value done
         """);
      var shell = Sandbox.ShellOfItsSessions with { CommandLine = $"pwsh.exe -NoLogo -NoProfile -NoExit -File \"{script}\"" };
      var hidden = Sandbox.HubOfTheSandbox.Hub.StartASessionHost(shell, _project.Project.Folder, _project.LabelOfCloydsHosts).GetAwaiter().GetResult();
      _hidden = hidden.Id;
      _project.SaveTheWindowAs(new SavedProjectWindow([new SavedSessionTab(selected.Id, selected.Directory, null),
                                                      new SavedSessionTab(hidden.Id, hidden.Directory, null)], selected.Id, null));
      _window = Open(_project.Window);
      WaitUntil(() => _window.BringingBackTheTabs.IsCompleted && _window.Tabs.Count == 2, "both tabs appeared");
   }

   [XF] public void its_shell_can_finish_writing_six_megabytes_without_a_terminal_reader()
   {
      File.WriteAllText(Path.Combine(_project.Project.Folder, "start-writing"), "");
      WaitUntil(() => File.Exists(Path.Combine(_project.Project.Folder, "finished-writing")), "the unselected shell finished producing more than the relay's 4 MiB limit");
      _project.SessionHost(_hidden)!.Attached.Must().BeFalse();
   }

   [XF] public void ending_its_shell_removes_the_hidden_tab_and_its_saved_tab()
   {
      _project.End(_hidden);
      WaitUntil(() => _window.Tabs.Count == 1, "the unselected shell disappeared");
      Close(_window);
      _project.SavedWindow.Tabs.Any(it => it.SessionHost == _hidden).Must().BeFalse();
   }

   [XF] public void an_external_attach_client_keeps_the_hidden_host()
   {
      using var external = AttachmentToASessionHost.To(_hidden, ConsoleSize.Default);
      WaitUntil(() => _window.Tabs.All(it => it.Host.Id != _hidden), "Cloyd released the externally claimed tab");
      external.EndsWithin(TimeSpan.Zero).Must().BeFalse();
   }
}
