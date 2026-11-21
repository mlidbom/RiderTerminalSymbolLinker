using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Cloyd.Chat.Status;

/// <summary>
/// The spinner that shows Claude works, as Claude Code's own: its glyphs in Claude's orange, one after another and back,
/// while it <see cref="IsTurning"/>. At the start of the <see cref="WorkingLineOfTheChat"/>'s turn line, and in the
/// header of each tab whose Claude works. As high whichever glyph it shows: the glyphs come from fonts of different
/// heights, and the line jumped up and down with them (Magnus, 2026-11-21).
/// </summary>
sealed class SpinnerOfClaudeCode : TextBlock
{
   /// <summary>The glyphs, as Claude Code's spinner shows them: each in turn, then back.</summary>
   const string Glyphs = "·✢✳✶✻✽✻✶✳✢";

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
      Foreground = new SolidColorBrush(Color.Parse("#D97757"));
      FontSize = SizeOfItsGlyphs;
      Width = HowWideItIs;
      Height = HowHighItIs;
      LineHeight = HowHighItIs;
      VerticalAlignment = VerticalAlignment.Center;
      Text = Glyphs[0].ToString();
      IsVisible = false;
      _turning.Tick += (_, _) => Turn();
   }

   protected override Type StyleKeyOverride => typeof(TextBlock);

   /// <summary>Whether it shows, and turns: while Claude works.</summary>
   public bool IsTurning
   {
      get => _turning.IsEnabled;
      set
      {
         IsVisible = value;
         _turning.IsEnabled = value;
      }
   }

   /// <summary>Shows the next glyph.</summary>
   void Turn()
   {
      _glyph = (_glyph + 1) % Glyphs.Length;
      Text = Glyphs[_glyph].ToString();
   }
}
