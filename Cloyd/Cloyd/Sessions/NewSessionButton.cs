using Avalonia.Controls;
using Avalonia.Input;

namespace Cloyd.Sessions;

/// <summary>
/// The button that starts a new session, at the start of the tab strip: a click opens a box for the session's name below
/// it. Enter starts the session with that name, or with none when the box is empty, and Escape starts none.
/// </summary>
sealed class NewSessionButton : Button
{
   readonly TextBox _name = new() { PlaceholderText = "The new session's name, or none", Width = 360 };

   public NewSessionButton()
   {
      ToolTip.SetTip(this, "New session");
      var flyout = new Flyout { Content = _name, Placement = PlacementMode.BottomEdgeAlignedLeft };
      Flyout = flyout;
      flyout.Opened += (_, _) =>
      {
         _name.Text = "";
         _name.Focus();
      };
      _name.KeyDown += (_, key) =>
      {
         if(key.Key is not (Key.Enter or Key.Escape)) return;

         key.Handled = true;
         flyout.Hide();
         if(key.Key == Key.Enter) NewSessionAsked?.Invoke(new NewSession(AgentCli.ClaudeCode, NameIn(_name.Text)));
      };
   }

   protected override Type StyleKeyOverride => typeof(Button);

   /// <summary>Raised with the new session Magnus asked for.</summary>
   public event Action<NewSession>? NewSessionAsked;

   static string? NameIn(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
