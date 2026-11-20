using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Cloyd.Projects;
using Cloyd.Theming;

namespace Cloyd.History;

/// <summary>
/// The session history picker: laid over a <see cref="ProjectWindow"/>, it lists the <see cref="ClaudeSessionHistory"/>,
/// most recently used first, for Magnus to resume a session from, or to bring forward the tab it runs in. Typing filters
/// it, and its <see cref="ClaudeSessionHistoryScope"/>s widen it from the project's worktree to every project. Enter, or a
/// double click, chooses the selected session, and Escape, or a click beside the picker, closes it. A session that runs
/// where no tab can show it is greyed out, and can't be chosen.
/// </summary>
/// <remarks>
/// Up and Down move the selection from the search box, as from the list: the picker takes them before either does.
/// The Kotlin plugin's <c>ClaudeSessionHistoryPicker</c>, ported (<c>dev_docs/claude-sessions.md</c>, "The session
/// history picker").
/// </remarks>
sealed class ClaudeSessionHistoryPicker : Panel
{
   /// <summary>The bold green of what tells that a session runs.</summary>
   static readonly ColourOfTheTheme Running = new("#2E7D32", "#6FBF73");

   /// <summary>The ground of the picker over the window.</summary>
   static readonly ColourOfTheTheme Ground = new("#FFFFFF", "#262624");

   /// <summary>The picker's border.</summary>
   static readonly ColourOfTheTheme Edge = new("#C9C8C4", "#4A4945");

   readonly ProjectWindow _pickedIn;
   readonly TextBox _search = new() { PlaceholderText = "Search the sessions: words of their titles, prompts, folders or branches" };
   readonly StackPanel _scopeButtons = new() { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
   readonly ListBox _list = new() { SelectionMode = SelectionMode.Single };
   readonly TextBlock _reading = new() { Text = "Reading the sessions…", Margin = new Thickness(12), Opacity = 0.7 };

   ClaudeSessionHistory? _history;

   /// <summary>A picker over <paramref name="pickedIn"/>, closed until <see cref="Open"/> opens it.</summary>
   public ClaudeSessionHistoryPicker(ProjectWindow pickedIn)
   {
      _pickedIn = pickedIn;
      IsVisible = false;
      Resources.MergedDictionaries.Add(ColourOfTheTheme.AsResources([Running, Ground, Edge]));
      _list.ItemTemplate = new FuncDataTemplate<ClaudeSessionHistoryEntry?>((entry, _) => entry is null ? new Panel() : RowOf(entry)); // The list builds a row with no entry as it swaps its entries.
      _list.DoubleTapped += (_, _) => ChooseTheSelected();
      _search.TextChanged += (_, _) => Refresh();
      AddHandler(KeyDownEvent, TakeAKey, RoutingStrategies.Tunnel);
      Children.Add(TheShadeOverTheWindow());
      Children.Add(TheCard());
   }

   /// <summary>Whether the picker is open over its window.</summary>
   public bool IsOpen => IsVisible;

   /// <summary>The scope the picker lists: <see cref="ClaudeSessionHistoryScope.ThisWorktree"/> as it opens.</summary>
   public ClaudeSessionHistoryScope Scope { get; private set; } = ClaudeSessionHistoryScope.ThisWorktree;

   /// <summary>The scopes the picker offers: all worktrees only for a project in git. None until it has read the history.</summary>
   public IReadOnlyList<ClaudeSessionHistoryScope> Scopes => _history?.Worktrees.Scopes ?? [];

   /// <summary>What is typed into the search box, which filters the entries.</summary>
   public string Filter
   {
      get => _search.Text ?? "";
      set => _search.Text = value;
   }

   /// <summary>The entries the picker lists, in their order: none until it has read the history.</summary>
   public IReadOnlyList<ClaudeSessionHistoryEntry> Listed => _list.ItemsSource as IReadOnlyList<ClaudeSessionHistoryEntry> ?? [];

   /// <summary>The listed entry that is selected, by its place: -1 when none is.</summary>
   public int Selected
   {
      get => _list.SelectedIndex;
      set => _list.SelectedIndex = value;
   }

   /// <summary>Whether the search box has the keyboard focus, as it has once the picker opens.</summary>
   public bool TheSearchBoxHasTheFocus => _search.IsFocused;

   /// <summary>Raised once the picker has closed for an entry Magnus chose, one that can be chosen, with the entry.</summary>
   public event Action<ClaudeSessionHistoryEntry>? Chosen;

   /// <summary>Raised once the picker has closed, whether for a choice or not.</summary>
   public event Action? Closed;

   /// <summary>Opens the picker over its window, reading the history until <see cref="Show"/> shows it, with the keyboard focus in the search box.</summary>
   public void Open()
   {
      _history = null;
      Scope = ClaudeSessionHistoryScope.ThisWorktree;
      _search.Text = "";
      _scopeButtons.Children.Clear();
      _list.ItemsSource = null;
      _reading.IsVisible = true;
      IsVisible = true;
      Dispatcher.UIThread.Post(() => _search.Focus(), DispatcherPriority.Loaded);
   }

   /// <summary>Shows <paramref name="history"/>, as read for the window's project, in the open picker.</summary>
   public void Show(ClaudeSessionHistory history)
   {
      _history = history;
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

      Refresh();
   }

   /// <summary>Lists the entries <paramref name="scope"/> covers, as a click on its button does, and gives the search box the focus back.</summary>
   public void List(ClaudeSessionHistoryScope scope)
   {
      Scope = scope;
      foreach(var button in _scopeButtons.Children.OfType<RadioButton>()) button.IsChecked = Equals(button.Content, scope.Label);
      Refresh();
      _search.Focus();
   }

   /// <summary>Chooses the selected entry, as Enter does: closes the picker and tells of the choice, unless the entry can't be chosen.</summary>
   public void ChooseTheSelected()
   {
      if(_list.SelectedItem is not ClaudeSessionHistoryEntry { Where.CanBeChosen: true } chosen) return;

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

   void Refresh()
   {
      if(_history is null) return;

      var listed = _history.Listed(Scope, Filter);
      _list.ItemsSource = listed;
      _list.SelectedIndex = listed.Count > 0 ? 0 : -1;
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
      if(Listed.Count == 0) return;

      _list.SelectedIndex = Math.Clamp(_list.SelectedIndex + by, 0, Listed.Count - 1);
      _list.ScrollIntoView(_list.SelectedIndex);
   }

   /// <summary>
   /// The row of <paramref name="entry"/>: its title, and where it is, in green while it runs; below, greyed, its last
   /// prompt, when it was last used, and, in a scope wider than the project's worktree, its folder and branch. One that
   /// can't be chosen is greyed out whole.
   /// </summary>
   Control RowOf(ClaudeSessionHistoryEntry entry)
   {
      var title = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, FontWeight = FontWeight.SemiBold };
      title.Inlines!.Add(new Run(OneLine(entry.Title)));
      if(entry.Where.AsToldIn(_pickedIn) is {} where)
      {
         var told = new Run("   " + where) { FontWeight = entry.Where.CanBeChosen ? FontWeight.Bold : FontWeight.Normal, FontStyle = entry.Where.CanBeChosen ? FontStyle.Normal : FontStyle.Italic };
         title.Inlines.Add(entry.Where is { IsRunning: true, CanBeChosen: true } ? Running.AsTheForegroundOf(told) : told);
      }

      var below = new TextBlock { Text = string.Join("   ·   ", WhatIsToldBelow()), TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 12, Opacity = 0.65 };
      return new StackPanel { Margin = new Thickness(2, 3), Spacing = 1, Opacity = entry.Where.CanBeChosen ? 1 : 0.5, Children = { title, below } };

      IEnumerable<string> WhatIsToldBelow()
      {
         if(entry.LastPrompt is {} lastPrompt) yield return OneLine(lastPrompt);
         yield return entry.LastUsedAsToldAt(DateTime.Now);
         if(Scope != ClaudeSessionHistoryScope.ThisWorktree && entry.Directory is {} directory) yield return entry.Branch is {} branch ? $"{directory} ({branch})" : directory;
      }
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
      var top = new DockPanel { Margin = new Thickness(0, 0, 0, 8), Children = { Docked(heading, Dock.Left), Docked(_scopeButtons, Dock.Right) } };
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
