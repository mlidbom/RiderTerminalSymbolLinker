using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Cloyd.Chat.Status;

/// <summary>
/// The spinner at the start of the <see cref="WorkingLineOfTheChat"/>, as Claude Code's own: its glyphs in Claude's
/// orange, one after another and back, each time it <see cref="Turn"/>s. As high whichever it shows: the glyphs come from
/// fonts of different heights, and the line jumped up and down with them (Magnus, 2026-11-21).
/// </summary>
sealed class SpinnerOfTheWorkingLine : TextBlock
{
   /// <summary>The glyphs, as Claude Code's spinner shows them: each in turn, then back.</summary>
   const string Glyphs = "·✢✳✶✻✽✻✶✳✢";

   /// <summary>How high it is, whichever glyph it shows, in logical pixels: about a line of the working line's text.</summary>
   const double HowHighItIs = 18;

   int _glyph;

   public SpinnerOfTheWorkingLine()
   {
      Foreground = new SolidColorBrush(Color.Parse("#D97757"));
      FontSize = 14;
      Width = 18;
      Height = HowHighItIs;
      LineHeight = HowHighItIs;
      VerticalAlignment = VerticalAlignment.Center;
      Text = Glyphs[0].ToString();
   }

   protected override Type StyleKeyOverride => typeof(TextBlock);

   /// <summary>Shows the next glyph.</summary>
   public void Turn()
   {
      _glyph = (_glyph + 1) % Glyphs.Length;
      Text = Glyphs[_glyph].ToString();
   }
}
