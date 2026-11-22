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
/// The blue dot that identifies Codex in a session tab and beside the activity in
/// <see cref="Chat.Status.ProgressOfACodexChat"/>. It rests at its smaller size between turns.
/// </summary>
sealed class CodexProgressDot : Border
{
   const string Pulsing = ":pulsing";
   const double ScaleAtRest = 0.62;

   public CodexProgressDot()
   {
      Width = Height = 14;
      Margin = new Thickness(2);
      HorizontalAlignment = HorizontalAlignment.Center;
      VerticalAlignment = VerticalAlignment.Center;
      CornerRadius = new CornerRadius(7);
      Resources.MergedDictionaries.Add(ColourOfTheTheme.AsResources([ColoursOfTheChat.CodexActivity]));
      ColoursOfTheChat.CodexActivity.AsTheBackgroundOf(this);
      RenderTransform = new ScaleTransform(ScaleAtRest, ScaleAtRest);
      Styles.Add(new Style(it => it.Class(Pulsing))
      {
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
