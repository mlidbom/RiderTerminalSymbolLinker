using Cloyd.Projects;
using Cloyd.Projects.Git;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

public sealed class When_Git_reports_the_state_of_dashboard_worktrees : IDisposable
{
   readonly GitRepositoryOfASpecification _repository = GitRepositoryOfASpecification.In(Directory.CreateTempSubdirectory("Cloyd-git-status-"));
   readonly Project _root;

   public When_Git_reports_the_state_of_dashboard_worktrees()
   {
      _root = new Project(_repository.Folder.FullName);
      _repository.Git("config", "user.name", "Specification");
      _repository.Git("config", "user.email", "specification@localhost");
      _repository.Git("config", "commit.gpgsign", "false");
      _repository.Git("symbolic-ref", "HEAD", "refs/heads/main");
   }

   [XF] public async Task feature_commits_are_compared_with_local_main_and_main_with_its_local_tracking_ref()
   {
      Commit("Initial");
      TrackRemoteMain();
      var synced = await Read();
      synced.Integrated.Must().Be(true);
      synced.Arrows.Must().Be("");
      _repository.Git("switch", "-qc", "topic");
      Commit("Feature");
      _repository.Git("update-ref", "refs/remotes/origin/topic", "HEAD");
      _repository.Git("branch", "--set-upstream-to=origin/topic", "topic");
      var feature = await Read();
      feature.Integrated.Must().Be(false);
      feature.Arrows.Must().Be("");
      _repository.Git("switch", "-q", "main");
      _repository.Git("merge", "-q", "--ff-only", "topic");
      (await Read()).Arrows.Must().Be("↑");
      _repository.Git("switch", "-q", "topic");
      (await Read()).Integrated.Must().Be(true);
      Commit("Remote work");
      _repository.Git("update-ref", "refs/remotes/origin/main", "HEAD");
      _repository.Git("switch", "-q", "main");
      var behind = await Read();
      behind.Integrated.Must().Be(true);
      behind.Arrows.Must().Be("↓");
      Commit("Local work");
      (await Read()).Arrows.Must().Be("↑↓");
   }

   [XF] public async Task staged_unstaged_and_untracked_files_count_once_per_changed_path_even_within_a_new_directory()
   {
      _repository.Track("unstaged.txt", "Initial");
      _repository.Track("both.txt", "Initial");
      Commit("Track both files");
      _repository.Write("unstaged.txt", "Unstaged change");
      _repository.Track("both.txt", "Staged change");
      _repository.Write("both.txt", "Also an unstaged change");
      _repository.Track("staged.txt", "New staged file");
      _repository.Write("untracked/one.txt", "");
      _repository.Write("untracked/two.txt", "");
      (await Read()).ChangedPaths.Must().Be(5);
   }

   [XF] public async Task an_unborn_checkout_and_a_missing_remote_tracking_ref_preserve_the_dirty_count()
   {
      _repository.Write("untracked.txt", "");
      var unborn = await Read();
      unborn.Integrated.Must().BeNull();
      unborn.ChangedPaths.Must().Be(1);
      Commit("Initial");
      var untrackedMain = await Read();
      untrackedMain.Integrated.Must().BeNull();
      untrackedMain.Arrows.Must().Be("");

      TrackRemoteMain();
      _repository.Git("update-ref", "-d", "refs/remotes/origin/main");
      var upstreamGone = await Read();
      upstreamGone.Integrated.Must().BeNull();
      upstreamGone.Arrows.Must().Be("");
      upstreamGone.ChangedPaths.Must().Be(1);
      upstreamGone.Description.Must().Satisfy(it => it.Contains("origin/main"));
   }

   [XF] public async Task detached_HEAD_compares_its_actual_commit_with_the_local_integration_branch()
   {
      Commit("Initial");
      _repository.Git("switch", "-q", "--detach");
      (await Read()).Integrated.Must().Be(true);
      Commit("Detached work");
      var detached = await Read();
      detached.Integrated.Must().Be(false);
      detached.Arrows.Must().Be("");
   }

   [XF] public async Task a_local_upstream_or_a_missing_integration_branch_has_no_remote_push_or_pull_status()
   {
      Commit("Initial");
      _repository.Git("branch", "other");
      _repository.Git("branch", "--set-upstream-to=other", "main");
      (await Read()).Integrated.Must().BeNull();
      _repository.Git("branch", "-m", "topic");
      var noIntegrationBranch = await Read();
      noIntegrationBranch.Integrated.Must().BeNull();
      noIntegrationBranch.Arrows.Must().Be("");
   }

   [XF] public async Task a_local_counterpart_of_origins_default_branch_is_the_target_when_main_is_absent()
   {
      Commit("Initial");
      _repository.Git("branch", "-m", "trunk");
      _repository.Git("branch", "master");
      _repository.Git("update-ref", "refs/remotes/origin/trunk", "HEAD");
      _repository.Git("symbolic-ref", "refs/remotes/origin/HEAD", "refs/remotes/origin/trunk");
      (await GitIntegrationBranch.Of(_root, default))!.Name.Must().Be("trunk");
   }

   async Task<WorktreeStatus> Read() => await WorktreeStatus.Read(new GitWorktree(_root, null, true), await GitIntegrationBranch.Of(_root, default), default);

   void TrackRemoteMain()
   {
      _repository.Git("remote", "add", "origin", _root.Folder);
      _repository.Git("update-ref", "refs/remotes/origin/main", "HEAD");
      _repository.Git("branch", "--set-upstream-to=origin/main", "main");
   }

   void Commit(string message) => _repository.Git("commit", "-q", "--allow-empty", "-m", message);
   public void Dispose() => _repository.Delete();
}
