using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Cloyd.Projects;
using Cloyd.Sessions;
using Cloyd.Theming;

namespace Cloyd.History;

/// <summary>
/// The session history picker: laid over a <see cref="ProjectWindow"/>, it lists the <see cref="SessionHistory"/>,
/// most recently used first, for Magnus to resume a session from, or to bring forward the tab it runs in. Typing filters
/// it, and its <see cref="SessionHistoryScope"/>s widen it from the project's worktree to every project. Enter, or a
/// double click, chooses the selected session, and Escape, or a click beside the picker, closes it. A session that runs
/// where no tab can show it is greyed out, and can't be chosen.
/// </summary>
/// <remarks>
/// The sessions of every agent CLI the hub serves are in one list, Claude Code's and Codex's interleaved, each marked with
/// its agent CLI, and a toggle for each agent CLI lists its sessions or leaves them out, each on as the picker opens.
/// From a hub that serves Claude Code alone, the picker shows neither marks nor toggles.
/// </remarks>
/// <remarks>
/// Up and Down move the selection from the search box, as from the list: the picker takes them before either does.
/// The Kotlin plugin's <c>ClaudeSessionHistoryPicker</c>, ported (<c>dev_docs/claude-sessions.md</c>, "The session
/// history picker").
/// </remarks>
sealed class SessionHistoryPicker : Panel
{
   /// <summary>The bold green of what tells that a session runs.</summary>
   static readonly ColourOfTheTheme Running = new("#2E7D32", "#6FBF73");

   /// <summary>The ground of the picker over the window.</summary>
   static readonly ColourOfTheTheme Ground = new("#FFFFFF", "#262624");

   /// <summary>The picker's border.</summary>
   static readonly ColourOfTheTheme Edge = new("#C9C8C4", "#4A4945");

   /// <summary>The ground of the mark of a Claude Code session: a tint of Claude's orange.</summary>
   static readonly ColourOfTheTheme MarkOfClaudeCode = new("#F6E3DA", "#4D3328");

   /// <summary>The ground of the mark of a Codex session: a tint of grey blue.</summary>
   static readonly ColourOfTheTheme MarkOfCodex = new("#DFE6EF", "#2E3846");

   readonly ProjectWindow? _pickedIn;
   readonly TextBox _search = new() { PlaceholderText = "Search the sessions: words of their titles, prompts, folders or branches" };
   readonly StackPanel _scopeButtons = new() { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
   readonly StackPanel _agentCliToggles = new() { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 24, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
   readonly SessionHistoryPickerViewModel _model;
   readonly ListBox _list = new() { SelectionMode = SelectionMode.Single };
   readonly TextBlock _reading = new() { Text = "Reading the sessions…", Margin = new Thickness(12), Opacity = 0.7 };

   /// <summary>A picker over <paramref name="pickedIn"/>, closed until <see cref="Open"/> opens it.</summary>
   public SessionHistoryPicker(ProjectWindow? pickedIn = null)
   {
      _pickedIn = pickedIn;
      _model = new SessionHistoryPickerViewModel(pickedIn is null ? SessionHistoryScope.AllProjects : SessionHistoryScope.ThisWorktree);
      IsVisible = false;
      Resources.MergedDictionaries.Add(ColourOfTheTheme.AsResources([Running, Ground, Edge, MarkOfClaudeCode, MarkOfCodex]));
      _list.ItemTemplate = new FuncDataTemplate<SessionHistoryEntry?>((entry, _) => entry is null ? new Panel() : RowOf(entry)); // The list builds a row with no entry as it swaps its entries.
      _list.DoubleTapped += (_, _) => ChooseTheSelected();
      _search.Bind(TextBox.TextProperty, CompiledBinding.Create((SessionHistoryPickerViewModel it) => it.Filter, _model,
                                                               mode: BindingMode.TwoWay, updateSourceTrigger: UpdateSourceTrigger.PropertyChanged));
      _list.Bind(ItemsControl.ItemsSourceProperty, CompiledBinding.Create((SessionHistoryPickerViewModel it) => it.Listed, _model));
      _list.Bind(SelectingItemsControl.SelectedIndexProperty, CompiledBinding.Create((SessionHistoryPickerViewModel it) => it.Selected, _model, mode: BindingMode.TwoWay));
      AddHandler(KeyDownEvent, TakeAKey, RoutingStrategies.Tunnel);
      Children.Add(TheShadeOverTheWindow());
      Children.Add(TheCard());
   }

   /// <summary>Whether the picker is open over its window.</summary>
   public bool IsOpen => IsVisible;

   /// <summary>The scope the picker lists. A project window starts in this worktree and Projects Dashboard starts with all projects.</summary>
   public SessionHistoryScope Scope => _model.Scope;

   /// <summary>The scopes the picker offers: all worktrees only for a project in git. None until it has read the history.</summary>
   public IReadOnlyList<SessionHistoryScope> Scopes => _model.Scopes;

   /// <summary>The agent CLIs whose sessions the picker has a toggle for: those the hub serves. None until it has read the history.</summary>
   public IReadOnlyList<AgentCli> AgentClis => _model.AgentClis;

   /// <summary>The agent CLIs whose sessions the picker lists: every one of <see cref="AgentClis"/> as it opens.</summary>
   public IReadOnlySet<AgentCli> AgentClisListed => _model.AgentClisListed;

   /// <summary>
   /// Whether the picker marks each entry with its agent CLI, and shows a toggle for each: while the hub serves more than one
   /// agent CLI.
   /// </summary>
   public bool TellsTheAgentClisApart => AgentClis.Count > 1;

   /// <summary>What is typed into the search box, which filters the entries.</summary>
   public string Filter
   {
      get => _model.Filter;
      set => _model.Filter = value;
   }

   /// <summary>The entries the picker lists, in their order: none until it has read the history.</summary>
   public IReadOnlyList<SessionHistoryEntry> Listed => _model.Listed;

   /// <summary>The listed entry that is selected, by its place: -1 when none is.</summary>
   public int Selected
   {
      get => _model.Selected;
      set => _model.Selected = value;
   }

   /// <summary>Whether the search box has the keyboard focus, as it has once the picker opens.</summary>
   public bool TheSearchBoxHasTheFocus => _search.IsFocused;

   /// <summary>Raised once the picker has closed for an entry Magnus chose, one that can be chosen, with the entry.</summary>
   public event Action<SessionHistoryEntry>? Chosen;

   /// <summary>Raised once the picker has closed, whether for a choice or not.</summary>
   public event Action? Closed;

   /// <summary>Opens the picker over its window, reading the history until <see cref="Show"/> shows it, with the keyboard focus in the search box.</summary>
   public void Open()
   {
      _model.Open();
      _scopeButtons.Children.Clear();
      _agentCliToggles.Children.Clear();
      _reading.IsVisible = true;
      IsVisible = true;
      Dispatcher.UIThread.Post(() => _search.Focus(), DispatcherPriority.Loaded);
   }

   /// <summary>Shows <paramref name="history"/>, as read for the window's project, in the open picker.</summary>
   public void Show(SessionHistory history)
   {
      _model.Show(history);
      _reading.IsVisible = false;
      foreach(var scope in history.Worktrees.Scopes)
      {
         var button = new RadioButton { Content = scope.Label, GroupName = $"scope-{GetHashCode()}", IsChecked = scope == Scope };
         button.IsCheckedChanged += (_, _) =>
         {
            if(button.IsChecked == true && scope != Scope) List(scope);
         };
         _scopeButtons.Children.Add(button);
      }

      foreach(var agentCli in history.AgentClis) _agentCliToggles.Children.Add(TheToggleOf(agentCli));

      _agentCliToggles.IsVisible = TellsTheAgentClisApart;
   }

   /// <summary>Lists the sessions of <paramref name="agentCli"/>, as switching its toggle on does, and gives the search box the focus back.</summary>
   public void ListTheSessionsOf(AgentCli agentCli) => TheToggleFor(agentCli).IsChecked = true;

   /// <summary>Leaves the sessions of <paramref name="agentCli"/> out, as switching its toggle off does, and gives the search box the focus back.</summary>
   public void LeaveOutTheSessionsOf(AgentCli agentCli) => TheToggleFor(agentCli).IsChecked = false;

   CheckBox TheToggleFor(AgentCli agentCli) => _agentCliToggles.Children.OfType<CheckBox>().Single(it => it.Tag == agentCli);

   /// <summary>The toggle that lists the sessions of <paramref name="agentCli"/> while it is on, as it is at first.</summary>
   CheckBox TheToggleOf(AgentCli agentCli)
   {
      var toggle = new CheckBox { Content = agentCli.Name, Tag = agentCli, IsChecked = true };
      toggle.IsCheckedChanged += (_, _) =>
      {
         _model.ListSessionsOf(agentCli, toggle.IsChecked == true);
         _search.Focus();
      };
      return toggle;
   }

   /// <summary>Lists the entries <paramref name="scope"/> covers, as a click on its button does, and gives the search box the focus back.</summary>
   public void List(SessionHistoryScope scope)
   {
      _model.List(scope);
      foreach(var button in _scopeButtons.Children.OfType<RadioButton>()) button.IsChecked = Equals(button.Content, scope.Label);
      _search.Focus();
   }

   /// <summary>Chooses the selected entry, as Enter does: closes the picker and tells of the choice, unless the entry can't be chosen.</summary>
   public void ChooseTheSelected()
   {
      if(_model.Choice is not {} chosen) return;

      Close();
      Chosen?.Invoke(chosen);
   }

   /// <summary>Closes the picker, choosing nothing, as Escape does.</summary>
   public void Close()
   {
      if(!IsVisible) return;

      IsVisible = false;
      Closed?.Invoke();
   }

   void TakeAKey(object? sender, KeyEventArgs key)
   {
      switch(key.Key)
      {
         case Key.Down: MoveTheSelection(1); break;
         case Key.Up: MoveTheSelection(-1); break;
         case Key.Enter: ChooseTheSelected(); break;
         case Key.Escape: Close(); break;
         default: return;
      }

      key.Handled = true;
   }

   void MoveTheSelection(int by)
   {
      _model.MoveSelection(by);
      if(_model.Selected >= 0) _list.ScrollIntoView(_model.Selected);
   }

   /// <summary>
   /// The row of <paramref name="entry"/>: the mark of its agent CLI, while the picker tells them apart; its title, and
   /// where it is, in green while it runs; below, greyed, its prompt, when it was last used, and, in a scope wider than the
   /// project's worktree, its folder and branch. One that can't be chosen is greyed out whole.
   /// </summary>
   Control RowOf(SessionHistoryEntry entry)
   {
      var title = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, FontWeight = FontWeight.SemiBold };
      title.Inlines!.Add(new Run(OneLine(entry.Title)));
      if(entry.Where.AsToldIn(_pickedIn) is {} where)
      {
         var told = new Run("   " + where) { FontWeight = entry.Where.CanBeChosen ? FontWeight.Bold : FontWeight.Normal, FontStyle = entry.Where.CanBeChosen ? FontStyle.Normal : FontStyle.Italic };
         title.Inlines.Add(entry.Where is { IsRunning: true, CanBeChosen: true } ? Running.AsTheForegroundOf(told) : told);
      }

      var below = new TextBlock { Text = string.Join("   ·   ", WhatIsToldBelow()), TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 12, Opacity = 0.65 };
      var top = new DockPanel { Children = { title } };
      if(TellsTheAgentClisApart) top.Children.Insert(0, Docked(TheMarkOf(entry.AgentCli), Dock.Left));
      return new StackPanel { Margin = new Thickness(2, 3), Spacing = 1, Opacity = entry.Where.CanBeChosen ? 1 : 0.5, Children = { top, below } };

      IEnumerable<string> WhatIsToldBelow()
      {
         if(entry.Prompt is {} prompt) yield return OneLine(prompt);
         yield return entry.LastUsedAsToldAt(DateTime.Now);
         if(Scope != SessionHistoryScope.ThisWorktree && entry.Directory is {} directory) yield return entry.Branch is {} branch ? $"{directory} ({branch})" : directory;
      }
   }

   /// <summary>The mark of an entry of <paramref name="agentCli"/>: its name, on a ground of its own, at the start of the entry's row.</summary>
   static Border TheMarkOf(AgentCli agentCli)
   {
      var mark = new Border
      {
         CornerRadius = new CornerRadius(3),
         Padding = new Thickness(6, 0),
         Margin = new Thickness(0, 0, 8, 0),
         MinWidth = 82,
         VerticalAlignment = VerticalAlignment.Center,
         Child = new TextBlock { Text = agentCli.Name, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center }
      };
      return (agentCli == AgentCli.Codex ? MarkOfCodex : MarkOfClaudeCode).AsTheBackgroundOf(mark);
   }

   /// <summary><paramref name="text"/> on one line, cut short where it would crowd out the rest of the row.</summary>
   static string OneLine(string text)
   {
      var oneLine = string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim();
      return oneLine.Length <= 160 ? oneLine : oneLine[..159] + "…";
   }

   /// <summary>The shade over the rest of the window, a click on which closes the picker.</summary>
   Control TheShadeOverTheWindow()
   {
      var shade = new Border { Background = new SolidColorBrush(Colors.Black, 0.35) };
      shade.PointerPressed += (_, _) => Close();
      return shade;
   }

   Control TheCard()
   {
      var heading = new TextBlock { Text = "Resume a session", FontWeight = FontWeight.SemiBold, FontSize = 15, VerticalAlignment = VerticalAlignment.Center };
      var top = new DockPanel { Margin = new Thickness(0, 0, 0, 8), Children = { Docked(heading, Dock.Left), Docked(_scopeButtons, Dock.Right), Docked(_agentCliToggles, Dock.Right) } };
      var search = new DockPanel { Margin = new Thickness(0, 0, 0, 8), Children = { _search } };
      var content = new DockPanel { Children = { Docked(top, Dock.Top), Docked(search, Dock.Top), Docked(_reading, Dock.Top), _list } };
      var card = new Border
      {
         BorderThickness = new Thickness(1),
         CornerRadius = new CornerRadius(8),
         Padding = new Thickness(12),
         Margin = new Thickness(40, 48),
         MaxWidth = 960,
         VerticalAlignment = VerticalAlignment.Top,
         Child = content
      };
      return Edge.AsTheBorderOf(Ground.AsTheBackgroundOf(card));
   }

   static Control Docked(Control control, Dock dock)
   {
      DockPanel.SetDock(control, dock);
      return control;
   }
}
