using ClaudeTooling.SessionHost.Protocol;

namespace Cloyd.Hub.Worktrees;

/// <summary>The result of applying a reviewed session disposition. A partial result explains
/// the effects already committed, and leaves the Git checkout available for another attempt.</summary>
sealed record WorktreeSessionsResult(Guid OperationId, string Status, SessionDispositionReceipt[] Results, string[] ClosedHostIds, RemovalBlocker[]? Blockers = null)
{
   public void RecordConfirmedClosures(WorktreeSessionsPreview preview, string? disposition, Action<SessionHostId> forgetClosedHost)
   {
      if(OperationId != preview.OperationId) throw new InvalidDataException("The hub returned a receipt for a different worktree operation.");
      var reviewed = preview.Sessions.Select(it => (it.AgentCli, it.SessionId)).ToHashSet();
      if(ClosedHostIds.Except(preview.HostIds).Any() || Results.Any(it => !reviewed.Contains((it.AgentCli, it.SessionId))))
         throw new InvalidDataException("The hub reported a target outside the reviewed worktree.");
      if(ClosedHostIds.Distinct().Count() != ClosedHostIds.Length || Results.DistinctBy(it => (it.AgentCli, it.SessionId)).Count() != Results.Length ||
         Results.DistinctBy(it => it.ArchiveEntryId).Count() != Results.Length || Results.Any(it => it.Disposition != disposition ||
            string.IsNullOrWhiteSpace(it.ArchiveEntryId) || (it.RecoverableUntil is not null) != (disposition == "recoverableDelete")))
         throw new InvalidDataException("The archive receipts do not match the reviewed session disposition.");
      foreach(var id in ClosedHostIds) forgetClosedHost(new SessionHostId(id));
   }

   public void RequireComplete(WorktreeSessionsPreview preview)
   {
      if(Status != "completed" || Blockers is { Length: > 0 })
         throw new InvalidOperationException($"Worktree kept. {Results.Length} session dispositions completed and {ClosedHostIds.Length} hosts closed. " +
                                             string.Join(Environment.NewLine, (Blockers ?? []).Select(it => it.Message)));
      if(preview.Sessions.Length != Results.Length || preview.HostIds.Count != ClosedHostIds.Length)
         throw new InvalidDataException("Worktree kept. The hub did not confirm every reviewed session and shell.");
   }
}

sealed record SessionDispositionReceipt(string AgentCli, string SessionId, string ArchiveEntryId, string Disposition, DateTimeOffset? RecoverableUntil);
