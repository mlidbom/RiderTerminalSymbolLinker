using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace Cloyd.Sessions;

/// <summary>
/// The button that starts a new session, at the start of the tab strip: a click opens a box below it, with the agent
/// CLIs it offers, Claude Code chosen, and the session's name. Enter starts a session of the chosen agent CLI with that
/// name, or with none when the name is empty, and Escape starts none.
/// </summary>
/// <remarks>
/// It offers what the window tells it as the box opens (<see cref="Opened"/>, <see cref="Offer"/>): Codex only from a hub
/// that serves Codex sessions. While it offers one agent CLI only, the box shows no choice.
/// </remarks>
sealed class NewSessionButton : Button
{
   readonly TextBox _name = new() { PlaceholderText = "The new session's name, or none", Width = 360 };
   readonly StackPanel _choiceOfAgentCli = new() { Orientation = Orientation.Horizontal, Spacing = 12 };

   public NewSessionButton()
   {
      ToolTip.SetTip(this, "New session");
      var flyout = new Flyout { Content = new StackPanel { Spacing = 6, Children = { _choiceOfAgentCli, _name } }, Placement = PlacementMode.BottomEdgeAlignedLeft };
      Flyout = flyout;
      flyout.Opened += (_, _) =>
      {
         _name.Text = "";
         _name.Focus();
         Opened?.Invoke();
      };
      _name.KeyDown += (_, key) =>
      {
         if(key.Key is not (Key.Enter or Key.Escape)) return;

         key.Handled = true;
         flyout.Hide();
         if(key.Key == Key.Enter) NewSessionAsked?.Invoke(new NewSession(Chosen, NameIn(_name.Text)));
      };
      Offer([AgentCli.ClaudeCode]);
   }

   protected override Type StyleKeyOverride => typeof(Button);

   /// <summary>Raised as its box opens, for the window to <see cref="Offer"/> the agent CLIs the hub serves.</summary>
   public event Action? Opened;

   /// <summary>Raised with the new session Magnus asked for.</summary>
   public event Action<NewSession>? NewSessionAsked;

   /// <summary>The agent CLIs it offers, the first chosen as the box opens.</summary>
   public IReadOnlyList<AgentCli> Offered { get; private set; } = [];

   /// <summary>The agent CLI of the session Enter starts.</summary>
   public AgentCli Chosen { get; private set; } = AgentCli.ClaudeCode;

   /// <summary>Whether its box shows the choice between the agent CLIs it offers: while it offers more than one.</summary>
   public bool ShowsTheChoiceOfAgentCli => _choiceOfAgentCli.IsVisible;

   /// <summary>Offers <paramref name="agentClis"/>, the first chosen.</summary>
   public void Offer(IReadOnlyList<AgentCli> agentClis)
   {
      Offered = agentClis;
      Chosen = agentClis[0];
      _choiceOfAgentCli.Children.Clear();
      foreach(var agentCli in agentClis) _choiceOfAgentCli.Children.Add(TheChoiceOf(agentCli));
      _choiceOfAgentCli.IsVisible = agentClis.Count > 1;
   }

   /// <summary>Chooses <paramref name="agentCli"/>, one it <see cref="Offered"/>, as a click on its choice does.</summary>
   public void Choose(AgentCli agentCli) => _choiceOfAgentCli.Children.OfType<RadioButton>().Single(it => it.Tag == agentCli).IsChecked = true;

   /// <summary>The radio button choosing <paramref name="agentCli"/>, which gives the keyboard back to the name once clicked.</summary>
   RadioButton TheChoiceOf(AgentCli agentCli)
   {
      var choice = new RadioButton { Content = agentCli.Name, Tag = agentCli, GroupName = nameof(AgentCli), IsChecked = agentCli == Chosen };
      choice.IsCheckedChanged += (_, _) =>
      {
         if(choice.IsChecked != true) return;

         Chosen = agentCli;
         _name.Focus();
      };
      return choice;
   }

   static string? NameIn(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
