using Cloyd.Projects;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace Cloyd.ProjectsDashboard;

/// <summary>A repository and its worktrees, a project outside Git, or the read-only group of sessions outside hosts.</summary>
sealed class DashboardProject(Project? project) : DashboardGroup(KeyFor(project))
{
   public Project? Project { get; } = project;
   /// <summary>The muted group colour used by this project's heading and the guide beside its worktrees and tabs.</summary>
   public IBrush GroupColour { get; } = ColourFor(project);
   public static string KeyFor(Project? project) => project is null ? "outside-hosts" : $"project:{project.Folder}";
   public override string Name => Project is null ? "Outside session hosts" : Project.Name.Length > 0 ? Project.Name : Project.Folder;
   public override string Description => Project?.Folder ?? Name;
   public override Project? StartFolder => Children.OfType<DashboardWorktree>().Any() ? null : Project;
   public void UpdateStartFolder() => OnPropertyChanged(nameof(CanStartSession));

   static IBrush ColourFor(Project? project)
   {
      string[] colours = ["#548A9E", "#9681AF", "#748C68", "#9F806E", "#6E87B5", "#58938B"];
      var index = project is null ? 0 : SHA256.HashData(Encoding.UTF8.GetBytes(project.Folder.ToUpperInvariant()))[0] % colours.Length;
      return new ImmutableSolidColorBrush(Color.Parse(project is null ? "#808080" : colours[index]));
   }
}
