using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Cloyd.Chat.Views;
using Cloyd.Theming;

namespace Cloyd.Codex;

/// <summary>
/// The dot that identifies Codex in a session tab and beside the activity in
/// <see cref="Chat.Status.ProgressOfACodexChat"/>. It rests in grey between turns and pulses in blue during a turn.
/// </summary>
sealed class CodexProgressDot : Border
{
   const string Pulsing = ":pulsing";
   /// <summary>The full diameter in logical pixels, matching the largest glyphs drawn by <see cref="Cloyd.Chat.Status.SpinnerOfClaudeCode"/>.</summary>
   const double Diameter = 11;
   const double ScaleAtRest = 0.7;

   public CodexProgressDot()
   {
      Width = Height = Diameter;
      Margin = new Thickness(3.5);
      HorizontalAlignment = HorizontalAlignment.Center;
      VerticalAlignment = VerticalAlignment.Center;
      CornerRadius = new CornerRadius(Diameter / 2);
      Resources.MergedDictionaries.Add(ColourOfTheTheme.AsResources([ColoursOfTheChat.Grey, ColoursOfTheChat.CodexActivity]));
      Styles.Add(new Style { Setters = { ColoursOfTheChat.Grey.AsASetterFor(BackgroundProperty) } });
      RenderTransform = new ScaleTransform(ScaleAtRest, ScaleAtRest);
      Styles.Add(new Style(it => it.Class(Pulsing))
      {
         Setters = { ColoursOfTheChat.CodexActivity.AsASetterFor(BackgroundProperty) },
         Animations =
         {
            new Animation
            {
               Duration = TimeSpan.FromSeconds(1.2), PlaybackDirection = PlaybackDirection.Alternate,
               IterationCount = IterationCount.Infinite, Easing = new SineEaseInOut(),
               Children = { At(0, ScaleAtRest), At(1, 1) }
            }
         }
      });

      static KeyFrame At(double cue, double scale) =>
         new() { Cue = new Cue(cue), Setters = { new Setter(ScaleTransform.ScaleXProperty, scale), new Setter(ScaleTransform.ScaleYProperty, scale) } };
   }

   /// <summary>While Codex works, the dot grows and shrinks around its centre, keeping the text beside it in place.</summary>
   public bool IsPulsing
   {
      get => Classes.Contains(Pulsing);
      set => PseudoClasses.Set(Pulsing, value);
   }
}
