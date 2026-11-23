using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Cloyd.Projects;
using Cloyd.Projects.Git;
using Cloyd.Hub.Worktrees;

namespace Cloyd.ProjectsDashboard;

/// <summary>Reviews both checkouts, their branches, saved conversations and live occupants before removal.</summary>
sealed partial class WorktreeRemovalDialog : Window
{
   protected override Type StyleKeyOverride => typeof(Window);
   readonly RemoveDashboardWorktree _removal;
   readonly Func<WorktreeSession, string> _nameOf;
   readonly Action<Project> _showChanges;
   readonly CancellationTokenSource _lifetime;
   readonly HashSet<Project> _deleteBranches = [];
   WorktreeRemovalPlan? _git;
   WorktreeRemovalReview? _review;
   string? _busy;
   string? _message;
   bool _failed;
   bool _deleteConversations;
   bool _mutating;
   readonly WorktreeStopFollowUp _afterStop = new(TimeProvider.System);
   CancellationTokenSource? _followingStop;

   public WorktreeRemovalDialog(RemoveDashboardWorktree removal, Func<WorktreeSession, string> nameOf,
                                Action<Project> showChanges, Func<ClaudeTooling.SessionHost.Protocol.SessionHostId, bool> hasDraft, CancellationToken cancellation)
   {
      _removal = removal;
      _nameOf = nameOf;
      _showChanges = showChanges;
      _hasDraft = hasDraft;
      _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
      Title = "Remove worktree";
      Width = 808;
      MinWidth = 520;
      SizeToContent = SizeToContent.Height;
      WindowStartupLocation = WindowStartupLocation.CenterOwner;
      ShowInTaskbar = false;
      Opened += (_, _) =>
      {
         if(Screens.ScreenFromWindow(this) is {} screen) MaxHeight = Math.Max(320, screen.WorkingArea.Height / screen.Scaling - 60);
         _ = Refresh();
      };
      Closing += (_, closing) => { if(_mutating && !cancellation.IsCancellationRequested) closing.Cancel = true; };
      Closed += (_, _) => _lifetime.Cancel();
      KeyDown += (_, key) => { if(key.Key == Key.Escape && !_mutating) Close(); };
      RenderReview();
   }

   async Task Refresh(string? outcome = null, bool failed = false)
   {
      if(_busy is not null || _lifetime.IsCancellationRequested) return;
      _followingStop?.Cancel();
      var previous = _git;
      var previousReview = _review;
      _message = outcome;
      _failed = failed;
      _review = null;
      _git = null;
      _busy = "Checking worktrees…";
      RenderReview();
      try
      {
         _git = await _removal.ReadGit(_lifetime.Token);
         _deleteBranches.RemoveWhere(folder => previous?.Checkouts.SingleOrDefault(it => it.Checkout.Root.Equals(folder))
            != _git.Checkouts.SingleOrDefault(it => it.Checkout.Root.Equals(folder)));
         _busy = "Checking sessions…";
         if(!_lifetime.IsCancellationRequested) RenderReview();
         ShowReading(await _removal.ReviewSessions(_git, _lifetime.Token), previousReview);
      }
      catch(OperationCanceledException) when(_lifetime.IsCancellationRequested) { }
      catch(Exception failure) { _failed = true; _message = string.Join(Environment.NewLine, new[] { outcome, failure.Message }.Where(it => it is not null)); }
      finally { _busy = null; if(!_lifetime.IsCancellationRequested) RenderReview(); }
      if(_review is {} ready && !_lifetime.IsCancellationRequested) _ = FollowStoppedAgents(ready);
   }

   void ShowReading(WorktreeRemovalReview next, WorktreeRemovalReview? previous)
   {
      if(previous is null || !previous.SavedIdentities.SetEquals(next.SavedIdentities)) _deleteConversations = false;
      _review = next;
   }

   async Task Act(string description, Func<Task<string?>> action, bool closeWhenDone = false)
   {
      if(_busy is not null || _review is null) return;
      _followingStop?.Cancel();
      _busy = description;
      _mutating = true;
      _message = null;
      RenderReview();
      string? outcome;
      var failed = false;
      try
      {
         outcome = await action();
         if(closeWhenDone)
         {
            _mutating = false;
            Close();
            return;
         }
      }
      catch(Exception failure) { outcome = failure.Message; failed = true; }
      finally { _busy = null; _mutating = false; }
      await Refresh(outcome, failed);
   }

   void RenderReview()
   {
      var body = Stack(16);
      var title = Stack(5);
      title.Children.Add(Label("Remove worktree", 27, FontWeight.SemiBold));
      title.Children.Add(Label(_removal.Worktree.Folder, 13, color: Muted));
      body.Children.Add(title);
      if(!string.IsNullOrEmpty(_message)) body.Children.Add(_failed ? Notice("Worktree review", _message, true) : Label(_message, 14));
      if(_git is {} git) body.Children.Add(Section("Git", GitCard(git)));
      if(_review is {} review)
      {
         foreach(var previous in review.Pending)
         {
            var operation = previous.Disposition switch { "archive" => "Archive", "recoverableDelete" => "Delete with seven-day recovery", _ => "Check vacancy" };
            body.Children.Add(Notice("Previous confirmation awaiting a receipt", $"{previous.Worktree} · {operation} · {previous.Sessions} saved conversations. Recovery replays that decision.", false));
            body.Children.Add(ActionButton("Recover previous confirmation", () => _ = Act("Recovering confirmation…", () => _removal.Recover(previous)), _busy is null));
         }
         body.Children.Add(Section("Sessions", SessionCard(review)));
         if(review.SavedCount > 0) body.Children.Add(Section("Saved conversations", SavedCard(review.SavedCount)));
      }
      if(_busy is not null) body.Children.Add(Label(_busy, 14, color: Muted));
      var layout = new DockPanel { Background = White };
      var footer = Footer();
      DockPanel.SetDock(footer, Dock.Bottom);
      layout.Children.Add(footer);
      layout.Children.Add(new ScrollViewer { Content = new Border { Padding = new Thickness(24,20,24,19), Child = body } });
      Content = layout;
   }

   Control Footer()
   {
      var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
      buttons.Children.Add(ActionButton("Refresh", () => _ = Refresh(), _busy is null));
      buttons.Children.Add(ActionButton("Cancel", Close, !_mutating));
      var count = _deleteBranches.Count;
      var label = count switch { 0 => _git?.SharedRules is null ? "Remove worktree" : "Remove worktrees", 1 => "Remove and delete 1 local branch", _ => $"Remove and delete {count} local branches" };
      var remove = ActionButton(label, () =>
      {
         if(_review is not { CanRemove: true } reviewed) return;
         _ = Act("Removing worktree…", async () =>
         {
            await _removal.Remove(reviewed, _deleteBranches, _deleteConversations, _lifetime.Token);
            return null;
         }, closeWhenDone: true);
      }, _busy is null && _review?.CanRemove == true);
      if(remove.IsEnabled)
      {
         remove.Background = _git!.Checkouts.Any(it => AtRisk(it) && DeletesCommitReference(it)) ? Red : Blue;
         remove.Foreground = Brushes.White;
      }
      buttons.Children.Add(remove);
      return new Border { BorderThickness = new Thickness(0,1,0,0), BorderBrush = Line, Background = Surface, Padding = new Thickness(22,12), Child = buttons };
   }

   Control SavedCard(int count)
   {
      var row = Stack(6);
      row.Children.Add(Label($"{count} saved {(count == 1 ? "conversation" : "conversations")}", 14, FontWeight.SemiBold));
      var options = new WrapPanel { Orientation = Orientation.Horizontal };
      var archive = new RadioButton { Content = "Archive", GroupName = "Disposition", IsChecked = !_deleteConversations, Margin = new Thickness(0,0,22,0) };
      var delete = new RadioButton { Content = "Delete · recoverable for 7 days", GroupName = "Disposition", IsChecked = _deleteConversations };
      archive.IsCheckedChanged += (_, _) => { if(archive.IsChecked == true) _deleteConversations = false; };
      delete.IsCheckedChanged += (_, _) => { if(delete.IsChecked == true) _deleteConversations = true; };
      options.Children.Add(archive);
      options.Children.Add(delete);
      row.Children.Add(options);
      return Card(new Border { IsEnabled = _busy is null, Padding = new Thickness(15,10), Child = row });
   }
}
