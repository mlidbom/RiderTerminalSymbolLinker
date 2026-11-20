using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Compze.xUnitBDD;
using Xunit;

namespace Cloyd.Specifications.Scenarios;

/// <summary>
/// The scenario that all the facts of one specification class share, when its constructors build it with
/// <see cref="SpecificationOpeningWindows.Once{TPart}"/>: built by the class's first fact, and ended after its last.
/// Without it, the scenario is built again for each fact, as xUnit makes a new instance of the class for each.
/// </summary>
/// <remarks>
/// Only for a class whose facts only look at what the scenario built: a fact that changes it changes it for the facts that
/// run after it.
/// </remarks>
/// <remarks>
/// Each part of it is what one call of <see cref="SpecificationOpeningWindows.Once{TPart}"/> in the class's constructors
/// built, kept by the call's place in the code: a nested class's scenario has a part built by its base class's constructor,
/// and its own. A part that fails ends the scenario at once, and each of the class's facts fails with its exception.<br/>
/// It ends once all the facts the class declares have run. When fewer run, as when a filter picks some of them, or when a
/// constructor fails after the scenario is built, it ends with the run: see <see cref="ScenariosLeftEndWithTheRun"/>.
/// </remarks>
sealed class ScenarioOfAClass
{
   static readonly ConcurrentDictionary<Type, ScenarioOfAClass> OfTheClasses = new();

   readonly Type _specification;
   readonly Dictionary<string, object?> _parts = [];
   readonly int _factsOfTheClass;
   int _factsDone;
   ExceptionDispatchInfo? _failure;

   ScenarioOfAClass(Type specification)
   {
      _specification = specification;
      _factsOfTheClass = FactsThatRunIn(specification);
   }

   /// <summary>The scenario of the class <paramref name="specification"/>: a new one, before its first fact.</summary>
   public static ScenarioOfAClass Of(Type specification) => OfTheClasses.GetOrAdd(specification, it => new ScenarioOfAClass(it));

   /// <summary>What the scenario showed and started, which ends with it.</summary>
   public WhatAScenarioStarted Started { get; } = new();

   /// <summary>
   /// The part of the scenario built at <paramref name="placeInTheCode"/>: <paramref name="built"/>'s, as the class's
   /// first fact builds it, and kept for the facts after it.
   /// </summary>
   public TPart Part<TPart>(string placeInTheCode, Func<TPart> built)
   {
      _failure?.Throw();
      if(_parts.TryGetValue(placeInTheCode, out var part)) return (TPart)part!;
      try
      {
         var builtNow = built();
         _parts.Add(placeInTheCode, builtNow);
         return builtNow;
      }
      catch(Exception failure)
      {
         _failure = ExceptionDispatchInfo.Capture(failure);
         Started.Dispose();
         throw;
      }
   }

   /// <summary>Tells the scenario that one more of the class's facts is done: after the last, it ends.</summary>
   public void AFactIsDone()
   {
      if(++_factsDone == _factsOfTheClass) End();
   }

   /// <summary>Ends the scenarios that haven't ended after their class's last fact.</summary>
   public static void EndThoseLeft()
   {
      foreach(var scenario in OfTheClasses.Values) scenario.End();
   }

   void End()
   {
      OfTheClasses.TryRemove(_specification, out _);
      Started.Dispose();
   }

   /// <summary>
   /// The facts that run in <paramref name="specification"/>, unless skipped: each xUnit <see cref="FactAttribute"/> it has,
   /// those of its base classes included, but an <see cref="ExclusiveFactAttribute"/>, such as an <see cref="XFAttribute"/>,
   /// only in the class that declares it, as xUnitBDD runs one.
   /// </summary>
   static int FactsThatRunIn(Type specification) =>
      specification.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                   .Count(it => it.GetCustomAttribute<FactAttribute>() is { Skip: null, Explicit: false } fact && (fact is not ExclusiveFactAttribute || it.DeclaringType == specification));
}
