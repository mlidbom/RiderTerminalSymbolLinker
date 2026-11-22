using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Cloyd.Chat.ToolCalls;
using Cloyd.Hub.RunningSessions;

namespace Cloyd.Chat.Status;

/// <summary>What the <see cref="WorkingLineOfTheChat"/> and <see cref="CallUnderWayOfTheChat"/> show
/// for one Claude Code session in every window. The hub supplies the running turn and its conversation
/// supplies the tool calls. The elapsed times advance while a window displays these rows.</summary>
sealed class ClaudeCodeTurnStatus : ObservableObject
{
   public const string Working = "Working.";

   readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
   RunningClaudeCode? _running;
   ToolCallsOfTheLastTurn _calls = new([]);
   DateTime? _busySince;
   int _loadedRows;

   public ClaudeCodeTurnStatus() => _clock.Tick += (_, _) => UpdateElapsedTimes();

   public bool IsBusy { get => field; private set => SetProperty(ref field, value); }
   public string? Activity { get => field; private set => SetProperty(ref field, value); }
   public string? TurnDetails { get => field; private set => SetProperty(ref field, value); }
   public bool HasCall { get => field; private set => SetProperty(ref field, value); }
   public string? CallDescription { get => field; private set => SetProperty(ref field, value); }
   public string? CallTime { get => field; private set => SetProperty(ref field, value); }
   public string? CallInput { get => field; private set => SetProperty(ref field, value); }
   public bool HasCallInput { get => field; private set => SetProperty(ref field, value); }

   /// <summary>Takes the hub's current <see cref="RunningClaudeCode"/>. When the hub only reports “busy,”
   /// elapsed time starts when this session first became busy, including before a window showed it.</summary>
   public void Show(RunningClaudeCode? running)
   {
      if(_running?.Process != running?.Process || _running?.SessionId != running?.SessionId)
      {
         _busySince = null;
         _calls = new([]);
      }
      _running = running;
      IsBusy = running?.IsBusy == true;
      _busySince = IsBusy ? _busySince ?? DateTime.UtcNow : null;
      Activity = IsBusy ? running?.TurnUnderWay?.Activity.AsShown ?? Working : null;
      UpdateCall();
      UpdateElapsedTimes();
      UpdateClock();
   }

   /// <summary>Reads the calls of the latest turn from its <paramref name="conversation"/>.</summary>
   public void Show(IReadOnlyList<ShownInTheChat> conversation)
   {
      _calls = ToolCallsOfTheLastTurn.In(conversation);
      UpdateCall();
      UpdateElapsedTimes();
   }

   /// <summary>A status row entered a window and needs elapsed-time updates.</summary>
   public void RowLoaded()
   {
      ++_loadedRows;
      UpdateElapsedTimes();
      UpdateClock();
   }

   /// <summary>A row left its window. After the last one leaves, the clock releases the session.</summary>
   public void RowUnloaded()
   {
      --_loadedRows;
      UpdateClock();
   }

   void UpdateClock() => _clock.IsEnabled = IsBusy && _loadedRows > 0;

   ToolCallInTheChat? CallUnderWay => IsBusy && _running?.TurnUnderWay?.Activity.IsRunningTools != false ? _calls.UnderWay : null;

   void UpdateCall()
   {
      var call = CallUnderWay;
      HasCall = call is not null;
      CallDescription = call?.WhatItDoes;
      CallInput = call?.WhatItWasGiven is {} given ? FirstLinesOfTheInput(given) : null;
      HasCallInput = CallInput is not null;
   }

   void UpdateElapsedTimes()
   {
      TurnDetails = _busySince is {} since ? $" ({string.Join(" · ", DetailsOfTheTurn(since))})" : null;
      CallTime = CallUnderWay is {} call ? $" · {TimeTaken.Since(call.MadeAt)}" : null;
   }

   IEnumerable<string> DetailsOfTheTurn(DateTime busySince)
   {
      var turn = _running?.TurnUnderWay;
      yield return TimeTaken.Since(turn?.StartedAt ?? busySince).ToString();
      if(turn?.OutputTokensSoFar is > 0 and var tokens) yield return $"↓ {InThousands(tokens)} tokens";
      if(_calls.Commands is > 0 and var commands) yield return commands == 1 ? "1 command" : $"{commands} commands";
   }

   static string InThousands(long tokens) => tokens < 1000 ? $"{tokens}" : (tokens / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + "k";

   /// <summary>The first three lines of a call's input, marked and truncated as Claude Code shows them.</summary>
   static string FirstLinesOfTheInput(string given)
   {
      const int linesShown = 3;
      var lines = given.Split('\n');
      var shown = lines.Take(linesShown).Select((line, at) => (at == 0 ? "└ " : "  ") + line).ToList();
      if(lines.Length > linesShown) shown[^1] += "…";
      return string.Join("\n", shown);
   }
}
