using Compze.Must;
using Compze.xUnitBDD;
using Cloyd.LocalApi.Contract;
using Cloyd.Projects;
using Cloyd.Specifications.TheSandbox;

namespace Cloyd.Specifications;

/// <summary>The Cloyd application runs with no window, and a project is handed to it, as <c>Cloyd.exe --project</c> hands it one.</summary>
public class When_a_project_is_handed_to_the_Cloyd_application : SpecificationRunningTheCloydApplication
{
   readonly SandboxProject _theProject;
   readonly SandboxProject _anotherProject;
   readonly WindowInTheAnswer _shown;

   public When_a_project_is_handed_to_the_Cloyd_application()
   {
      _theProject = Sandbox.Project("the-project");
      _anotherProject = Sandbox.Project("another-project");
      _shown = HandedOver(_theProject);
   }

   private protected override IReadOnlyList<SandboxProject> Projects => [_theProject, _anotherProject];

   [XF] public void it_opens_the_projects_window() => OnTheUIThread(() => Windows.Open.Select(it => it.Project).ToArray()).Must().SequenceEqual([_theProject.Project]);

   [XF] public void it_answers_with_the_window() => _shown.Must().Be(new WindowInTheAnswer(_theProject.Project.Folder, $"Cloyd: {_theProject.Project.Name}"));

   [XF] public void it_lists_the_window() => WindowsItLists().Must().SequenceEqual([_shown]);

   public class and_then_the_same_project_again : When_a_project_is_handed_to_the_Cloyd_application
   {
      readonly IReadOnlyList<ProjectWindow> _windowsBefore;

      public and_then_the_same_project_again()
      {
         _windowsBefore = OnTheUIThread(() => Windows.Open.ToArray());
         HandedOver(_theProject);
      }

      [XF] public void it_shows_the_window_it_has_open() => OnTheUIThread(() => Windows.Open.ToArray()).Must().SequenceEqual(_windowsBefore);
   }

   public class and_then_another_project : When_a_project_is_handed_to_the_Cloyd_application
   {
      public and_then_another_project() => HandedOver(_anotherProject);

      [XF] public void it_shows_a_window_for_each_in_the_order_they_were_handed_to_it() =>
         WindowsItLists().Select(it => it.Project).Must().SequenceEqual([_theProject.Project.Folder, _anotherProject.Project.Folder]);
   }

   public class and_its_window_closes : When_a_project_is_handed_to_the_Cloyd_application
   {
      public and_its_window_closes() => Close(OnTheUIThread(() => Windows.Open.Single()));

      [XF] public void it_lists_no_window() => WindowsItLists().Must().BeEmpty();
   }
}
