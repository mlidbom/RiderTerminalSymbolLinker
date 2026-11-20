using System.Collections.Concurrent;

namespace Cloyd.Specifications.TheSandbox;

/// <summary>
/// The <see cref="Sandbox"/>es of this run of the specifications, each started ahead of the scenario that takes it, and
/// ended in the background, so that neither a sandbox's start nor its end is on the scenarios' path: as a scenario takes
/// its sandbox, the next one starts, and a scenario's sandbox ends while the next scenario runs. Each scenario still has
/// a fresh sandbox of its own, never one another scenario used.
/// </summary>
/// <remarks>
/// On the scenarios' path, a single run's 68 sandboxes took 66 s of its 145 s: a start 0.42 s, a stop 0.16-0.33 s and a
/// deletion 0.04 s (2026-11-20).
/// </remarks>
/// <remarks>
/// <see cref="WhatEndsWithTheRun"/> waits as the run ends until every sandbox has ended, the one started ahead for no
/// scenario included, so a sandbox whose end failed fails the run there. The sandboxes of a run that was killed before
/// that, the next run ends as its first starts: see <see cref="Sandbox"/>.<br/>
/// A start that fails fails the scenario that takes the sandbox, as a start on the scenario's path did.
/// </remarks>
static class SandboxesOfThisRun
{
   static readonly ConcurrentQueue<Task> Ending = new();
   static Task<Sandbox>? _startedAhead;

   /// <summary>
   /// A fresh sandbox, once its hub runs: the one started ahead, with the next one's start begun. The run's first starts
   /// now, along with the one after it.
   /// </summary>
   public static Sandbox Next() => (Interlocked.Exchange(ref _startedAhead, StartedInTheBackground()) ?? StartedInTheBackground()).GetAwaiter().GetResult();

   /// <summary>Ends <paramref name="sandbox"/>, whose scenario has ended, in the background: its hub stops, and its folder is deleted.</summary>
   public static void EndInTheBackground(Sandbox sandbox) => Ending.Enqueue(Task.Run(sandbox.End));

   /// <summary>
   /// Waits until no sandbox is starting or ending in the background, as for measuring what else the run's process does.
   /// Their failures are told where they would be anyway: a start's to the scenario that takes the sandbox, an end's as
   /// the run ends.
   /// </summary>
   public static void WaitUntilNoneIsStartingOrEnding() =>
      Task.WhenAll(Ending.Append(_startedAhead ?? Task.CompletedTask)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing).GetAwaiter().GetResult();

   /// <summary>Ends the sandbox started ahead for no scenario, if one was, and waits until every sandbox has ended: one whose end failed fails it.</summary>
   public static void WaitUntilAllHaveEnded()
   {
      if(Interlocked.Exchange(ref _startedAhead, null) is {} unused) Ending.Enqueue(Task.Run(() => unused.GetAwaiter().GetResult().End()));
      Task.WaitAll([.. Ending]);
   }

   static Task<Sandbox> StartedInTheBackground() => Task.Run(Sandbox.StartedNow);
}
