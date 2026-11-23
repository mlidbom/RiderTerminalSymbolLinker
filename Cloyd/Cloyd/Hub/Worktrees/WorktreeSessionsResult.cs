namespace Cloyd.Hub.Worktrees;

/// <summary>The result of applying a reviewed session disposition. A partial result explains
/// the effects already committed, and leaves the Git checkout available for another attempt.</summary>
sealed record WorktreeSessionsResult(Guid OperationId, string Status, SessionDispositionReceipt[] Results, string[] ClosedHostIds, RemovalBlocker[]? Blockers = null)
{
   public void ValidateReceipts(WorktreeSessionsPreview preview, string? disposition)
   {
      if(OperationId != preview.OperationId) throw new InvalidDataException("The hub returned a receipt for a different worktree operation.");
      if(ClosedHostIds.Length != 0) throw new InvalidDataException("The hub reported closing a host during a stopped-session removal.");
      var reviewed = preview.Sessions.Select(it => (it.AgentCli, it.SessionId)).ToHashSet();
      if(Results.Any(it => !reviewed.Contains((it.AgentCli, it.SessionId)))) throw new InvalidDataException("The hub reported a session outside the reviewed worktree.");
      if(Results.DistinctBy(it => (it.AgentCli, it.SessionId)).Count() != Results.Length ||
         Results.DistinctBy(it => it.ArchiveEntryId).Count() != Results.Length || Results.Any(it => it.Disposition != disposition ||
            string.IsNullOrWhiteSpace(it.ArchiveEntryId) || (it.RecoverableUntil is not null) != (disposition == "recoverableDelete")))
         throw new InvalidDataException("The archive receipts do not match the reviewed session disposition.");
   }

   public void RequireComplete(WorktreeSessionsPreview preview)
   {
      if(Status != "completed" || Blockers is { Length: > 0 })
         throw new InvalidOperationException($"Worktree kept. {Results.Length} of {preview.Sessions.Length} saved conversations processed. " +
                                             string.Join(Environment.NewLine, (Blockers ?? []).Select(it => it.Message)));
      if(preview.Sessions.Length != Results.Length)
         throw new InvalidDataException("Worktree kept. The hub did not confirm every saved conversation.");
   }
}

sealed record SessionDispositionReceipt(string AgentCli, string SessionId, string ArchiveEntryId, string Disposition, DateTimeOffset? RecoverableUntil);
