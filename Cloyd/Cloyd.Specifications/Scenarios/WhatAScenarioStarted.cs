using Avalonia.Controls;

namespace Cloyd.Specifications.Scenarios;

/// <summary>
/// What a scenario showed and started, which ends with it: the windows it showed, closed then, and what else it started,
/// such as a <see cref="TheSandbox.SandboxProject"/>, ended then, the last first.
/// </summary>
/// <remarks>
/// A scenario is what a specification's constructors set up for its facts to look at: one fact's own, or, built with
/// <see cref="SpecificationOpeningWindows.Once{TPart}"/>, the <see cref="ScenarioOfAClass"/> all the class's facts share.
/// </remarks>
sealed class WhatAScenarioStarted : IDisposable
{
   readonly List<Window> _windows = [];
   readonly Stack<IDisposable> _started = new();

   /// <summary>The windows the scenario showed, to be closed as it ends.</summary>
   public IReadOnlyList<Window> Windows => _windows;

   /// <summary>Has <paramref name="window"/>, which the scenario showed, close as it ends.</summary>
   public void Showed(Window window) => _windows.Add(window);

   /// <summary>Has <paramref name="started"/> end as the scenario ends, after its windows close, and before what it started earlier.</summary>
   public void Started(IDisposable started) => _started.Push(started);

   /// <summary>Closes the windows, then ends what else the scenario started, the last first: once, however often it's called.</summary>
   public void Dispose()
   {
      TheHeadlessUIThread.Run(() =>
      {
         foreach(var window in _windows) window.Close();
         _windows.Clear();
      });
      while(_started.TryPop(out var started)) started.Dispose();
   }
}
