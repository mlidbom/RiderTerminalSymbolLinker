using XTerm;
using XTerm.Parser;

namespace Cloyd.Terminals;

/// <summary>
/// The mouse encodings that a program in a session host can ask the terminal for, but whose reports conhost, between the
/// two, can't read: urxvt's (mode <c>1015</c>) and SGR's in pixels (<c>1016</c>). A terminal of a session host ignores a
/// program's setting or resetting of them, and so keeps reporting the mouse in SGR's encoding, the one conhost asks for
/// as a program takes the mouse (<c>ESC [ ? 1003 ; 1006 h</c>).
/// </summary>
/// <remarks>
/// Conhost passes a mode it doesn't know on to the terminal as the program wrote it, and it knows neither of these. So a
/// program that turns them off as it lets go of the mouse, as crossterm does outside Windows, has <c>ESC [ ? 1015 l</c>
/// reach the terminal alone. XTerm.NET then reports in X10's encoding, where xterm resets only the encoding in effect.
/// Conhost can't read that either, and typed each report into the session as text: such as <c>CO!CL!CI!</c> in Codex's
/// input box as the mouse moved over its tab (2026-11-22). The session host sets the mode again in each terminal that
/// attaches to it later, so a tab shown for the first time got it too.
/// </remarks>
/// <remarks>
/// A sequence that sets or resets other modes with them is left to XTerm.NET, which sets them all, as no program has been
/// seen to write one.
/// </remarks>
static class MouseEncodingsConhostCantRead
{
   const int Urxvts = 1015;
   const int SgrsInPixels = 1016;

   /// <summary>Has <paramref name="terminal"/> ignore a program's setting or resetting of them from now on.</summary>
   public static void IgnoreIn(Terminal terminal)
   {
      terminal.RegisterCsiHandler("?h", IsOnlyThem);
      terminal.RegisterCsiHandler("?l", IsOnlyThem);
   }

   /// <summary>
   /// Whether <paramref name="modes"/>, the private modes a sequence sets or resets, are these alone: XTerm.NET passes such
   /// a sequence by, as its handler says it is handled.
   /// </summary>
   static bool IsOnlyThem(Params modes) => modes.ToArray().All(it => it is Urxvts or SgrsInPixels);
}
