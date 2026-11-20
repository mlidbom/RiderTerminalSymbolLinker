using Avalonia.Controls;
using Cloyd.Specifications.TheSandbox;

namespace Cloyd.Specifications.Scenarios;

/// <summary>
/// What a scenario showed and started, which ends with it: the windows it showed, closed then, what else it started, such
/// as a <see cref="StandInIde"/>, ended then, the last first, and last its <see cref="TheSandbox.Sandbox"/>.
/// </summary>
/// <remarks>
/// A scenario is what a specification's constructors set up for its facts to look at: one fact's own, or, built with
/// <see cref="SpecificationOpeningWindows.Once{TPart}"/>, the <see cref="ScenarioOfAClass"/> all the class's facts share.
/// </remarks>
sealed class WhatAScenarioStarted : IDisposable
{
   readonly List<Window> _windows = [];
   readonly Stack<IDisposable> _started = new();
   Sandbox? _sandbox;

   /// <summary>The windows the scenario showed, to be closed as it ends.</summary>
   public IReadOnlyList<Window> Windows => _windows;

   /// <summary>Has <paramref name="window"/>, which the scenario showed, close as it ends.</summary>
   public void Showed(Window window) => _windows.Add(window);

   /// <summary>The scenario's sandbox: started as the scenario first asks for it, and ended as the scenario ends, after all else it started.</summary>
   public Sandbox Sandbox => _sandbox ??= Sandbox.Started();

   /// <summary>Has <paramref name="started"/> end as the scenario ends, after its windows close, and before what it started earlier.</summary>
   public void Started(IDisposable started) => _started.Push(started);

   /// <summary>
   /// Closes the windows, then ends what else the scenario started, the last first, then its sandbox, which ends whatever
   /// still runs in it: once, however often it's called.
   /// </summary>
   public void Dispose()
   {
      try
      {
         TheHeadlessUIThread.Run(() =>
         {
            foreach(var window in _windows) window.Close();
            _windows.Clear();
         });
         while(_started.TryPop(out var started)) started.Dispose();
      }
      finally
      {
         _sandbox?.Dispose();
         _sandbox = null;
      }
   }
}
