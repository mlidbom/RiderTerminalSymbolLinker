using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using Cloyd.Sessions;

namespace Cloyd.History;

/// <summary>The entries one <see cref="SessionHistoryPicker"/> lists and the entry it selects.
/// Changing the search, scope or agent CLIs selects the most recent match.</summary>
sealed class SessionHistoryPickerViewModel : ObservableObject
{
   readonly SessionHistoryScope _initialScope;
   readonly HashSet<AgentCli> _agentClisListed = [];
   SessionHistory? _history;

   public SessionHistoryPickerViewModel(SessionHistoryScope initialScope) => Scope = _initialScope = initialScope;

   public SessionHistoryScope Scope { get; private set; }
   public IReadOnlyList<SessionHistoryScope> Scopes => _history?.Worktrees.Scopes ?? [];
   public IReadOnlyList<AgentCli> AgentClis => _history?.AgentClis ?? [];
   public IReadOnlySet<AgentCli> AgentClisListed => _agentClisListed;
   public IReadOnlyList<SessionHistoryEntry> Listed { get => field; private set => SetProperty(ref field, value); } = [];
   public int Selected { get => field; set => SetProperty(ref field, value); } = -1;
   [AllowNull] public string Filter
   {
      get => field;
      set { if(SetProperty(ref field, value ?? "")) ListMatches(); }
   } = "";

   /// <summary>The selected entry if it can be resumed or brought forward.</summary>
   public SessionHistoryEntry? Choice => Selected >= 0 && Selected < Listed.Count && Listed[Selected].Where.CanBeChosen ? Listed[Selected] : null;

   /// <summary>Starts a new reading, with the initial scope of this window and an empty search.</summary>
   public void Open()
   {
      _history = null;
      Scope = _initialScope;
      _agentClisListed.Clear();
      Filter = "";
      ListMatches();
   }

   public void Show(SessionHistory history)
   {
      _history = history;
      _agentClisListed.UnionWith(history.AgentClis);
      ListMatches();
   }

   public void List(SessionHistoryScope scope)
   {
      Scope = scope;
      ListMatches();
   }

   public void ListSessionsOf(AgentCli agentCli, bool include)
   {
      if(include) _agentClisListed.Add(agentCli);
      else _agentClisListed.Remove(agentCli);
      ListMatches();
   }

   public void MoveSelection(int by)
   {
      if(Listed.Count > 0) Selected = Math.Clamp(Selected + by, 0, Listed.Count - 1);
   }

   void ListMatches()
   {
      Listed = _history?.Listed(Scope, Filter, _agentClisListed) ?? [];
      Selected = Listed.Count > 0 ? 0 : -1;
   }
}
