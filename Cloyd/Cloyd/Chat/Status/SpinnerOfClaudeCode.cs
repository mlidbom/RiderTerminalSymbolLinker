using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Cloyd.Chat.Views;
using Cloyd.Theming;

namespace Cloyd.Chat.Status;

/// <summary>
/// The spinner that shows Claude works, as Claude Code's own: its glyphs in Claude's orange, one after another and back,
/// while it <see cref="IsTurning"/>. At the start of the <see cref="WorkingLineOfTheChat"/>'s turn line, and in the
/// header of each Claude Code tab, where it rests on a grey star between turns. The glyphs keep the same line height.
/// </summary>
sealed class SpinnerOfClaudeCode : TextBlock
{
   public static readonly StyledProperty<bool> IsTurningProperty = AvaloniaProperty.Register<SpinnerOfClaudeCode, bool>(nameof(IsTurning));

   /// <summary>The glyphs, as Claude Code's spinner shows them: each in turn, then back.</summary>
   const string Glyphs = "·✢✳✶✻✽✻✶✳✢";
   const string IdleStar = "✶";

   /// <summary>How high it is, whichever glyph it shows, in logical pixels: about a line of the working line's text.</summary>
   public const double HowHighItIs = 18;

   /// <summary>How wide it is, whichever glyph it shows, in logical pixels.</summary>
   public const double HowWideItIs = 18;

   /// <summary>The size of its glyphs' font.</summary>
   public const double SizeOfItsGlyphs = 14;

   /// <summary>
   /// Turns it while it turns: at a quarter of the speed of Claude Code's own spinner, as Magnus asked twice
   /// (2026-11-21, "halved maybe", then "Slow the animation by half again").
   /// </summary>
   readonly DispatcherTimer _turning = new() { Interval = TimeSpan.FromMilliseconds(480) };

   int _glyph;

   public SpinnerOfClaudeCode()
   {
      Resources.MergedDictionaries.Add(ColourOfTheTheme.AsResources([ColoursOfTheChat.IdleClaudeStar, ColoursOfTheChat.ClaudeActivity]));
      Styles.Add(new Style { Setters = { ColoursOfTheChat.IdleClaudeStar.AsASetterFor(ForegroundProperty) } });
      Styles.Add(new Style(it => it.PropertyEquals(IsTurningProperty, true)) { Setters = { ColoursOfTheChat.ClaudeActivity.AsASetterFor(ForegroundProperty) } });
      FontFamily = new FontFamily("Segoe UI Symbol");
      FontSize = SizeOfItsGlyphs;
      Width = HowWideItIs;
      Height = HowHighItIs;
      LineHeight = HowHighItIs;
      VerticalAlignment = VerticalAlignment.Center;
      TextAlignment = TextAlignment.Center;
      Text = IdleStar;
      _turning.Tick += (_, _) => Turn();
      Loaded += (_, _) => _turning.IsEnabled = IsTurning;
      Unloaded += (_, _) => _turning.Stop();
   }

   protected override Type StyleKeyOverride => typeof(TextBlock);

   /// <summary>Whether Claude is working. An attached spinner cycles through its glyphs until the turn ends.</summary>
   public bool IsTurning
   {
      get => GetValue(IsTurningProperty);
      set => SetValue(IsTurningProperty, value);
   }

   protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
   {
      base.OnPropertyChanged(change);
      if(change.Property != IsTurningProperty) return;
      _turning.IsEnabled = IsLoaded && IsTurning;
      if(!IsTurning) Text = IdleStar;
   }

   /// <summary>Shows the next glyph.</summary>
   void Turn()
   {
      _glyph = (_glyph + 1) % Glyphs.Length;
      Text = Glyphs[_glyph].ToString();
   }
}
