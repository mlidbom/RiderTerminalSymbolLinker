namespace Cloyd.ProjectsDashboard;

sealed partial class WorktreeRemovalDialog
{
   /// <summary>Leave the review usable while the stopped agent hands its terminal back to the shell.
   /// An explicit action or refresh supersedes any observation already underway.</summary>
   async Task FollowStoppedAgents(WorktreeRemovalReview review)
   {
      if(!_afterStop.StillWaiting(review.Occupants)) return;
      using var observing = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
      _followingStop = observing;
      try
      {
         var observed = _afterStop.Observe(review.Occupants);
         for(var delay = 1; ; delay = Math.Min(8, delay * 2))
         {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(observing.Token);
            var remaining = _afterStop.UntilNextExpiry;
            deadline.CancelAfter(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
            try
            {
               await Task.Delay(TimeSpan.FromSeconds(delay), deadline.Token);
               var current = await _afterStop.ReadOccupants(review, deadline.Token);
               deadline.Token.ThrowIfCancellationRequested();
               if(current.Matches(observed)) continue;
               var next = await _removal.ReviewSessions(review.Git, deadline.Token);
               deadline.Token.ThrowIfCancellationRequested();
               ShowReading(next, review);
               review = next;
               observed = current;
            }
            catch(OperationCanceledException) when(deadline.IsCancellationRequested && !observing.IsCancellationRequested) { }
            var waiting = _afterStop.StillWaiting(review.Occupants);
            RenderReview();
            if(!waiting) return;
         }
      }
      catch(OperationCanceledException) when(observing.IsCancellationRequested) { }
      catch(Exception failure)
      {
         if(observing.IsCancellationRequested) return;
         _afterStop.Finish();
         _review = null;
         _failed = true;
         _message = $"Could not recheck sessions. {failure.Message}";
         RenderReview();
      }
      finally { if(_followingStop == observing) _followingStop = null; }
   }
}
