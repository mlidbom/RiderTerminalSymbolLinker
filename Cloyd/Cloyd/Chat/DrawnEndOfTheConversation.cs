using System.Collections.ObjectModel;

namespace Cloyd.Chat;

/// <summary>
/// What the chat draws of what it shows: the last <see cref="DrawnAtATime"/> things of the conversation at first, and as
/// many more each time Magnus scrolls up to the first it draws. What the conversation adds is drawn as it comes.
/// </summary>
/// <remarks>
/// Drawn in full, as a plain list, so that the chat knows how long what it draws is: a virtualizing list, which draws only
/// what is in view, guesses the length of the rest, and its guesses moved the end of a long conversation without end, as
/// the heights in a chat range from a line to a screenful and change as Claude's markdown is laid out. Seen in Magnus's
/// own sessions, 2026-11-20: the chat flickered, and Cloyd stopped answering.
/// </remarks>
sealed class DrawnEndOfTheConversation
{
   /// <summary>How many things the chat draws at first, and draws more of each time Magnus scrolls to the top of them.</summary>
   public const int DrawnAtATime = 100;

   readonly ObservableCollection<ShownInTheChat> _drawn = [];

   public DrawnEndOfTheConversation() => Drawn = new ReadOnlyObservableCollection<ShownInTheChat>(_drawn);

   /// <summary>What the chat shows of the conversation, in order, drawn or not.</summary>
   public IReadOnlyList<ShownInTheChat> Shown { get; private set; } = [];

   /// <summary>What it draws: the end of <see cref="Shown"/>, as it changes.</summary>
   public ReadOnlyObservableCollection<ShownInTheChat> Drawn { get; }

   /// <summary>Whether there is more of the conversation above what is drawn.</summary>
   public bool HasMoreAbove => FirstDrawn > 0;

   /// <summary>Where in <see cref="Shown"/> what is drawn starts.</summary>
   public int FirstDrawn => Shown.Count - _drawn.Count;

   /// <summary>
   /// Shows <paramref name="now"/> in place of what it shows: redraws only what differs, from the first thing that does on,
   /// as what the conversation adds is at its end. It draws the end of <paramref name="now"/> anew when what differs
   /// starts above what is drawn, as after a rewind, and when more than <see cref="DrawnAtATime"/> things differ, as when
   /// the hub's stream comes back after a while: so what is drawn grows by more than that only as Magnus scrolls up.
   /// </summary>
   public void Show(IReadOnlyList<ShownInTheChat> now)
   {
      var same = 0;
      while(same < Shown.Count && same < now.Count && Shown[same].Equals(now[same])) same++;
      var firstDrawn = FirstDrawn;
      Shown = now;
      if(_drawn.Count == 0 || same < firstDrawn || now.Count - same > DrawnAtATime)
      {
         _drawn.Clear();
         foreach(var drawn in now.Skip(Math.Max(0, now.Count - DrawnAtATime))) _drawn.Add(drawn);
         return;
      }

      while(_drawn.Count > same - firstDrawn) _drawn.RemoveAt(_drawn.Count - 1);
      foreach(var added in now.Skip(same)) _drawn.Add(added);
   }

   /// <summary>Draws more above what is drawn until the thing at <paramref name="shown"/> in <see cref="Shown"/> is drawn, as a search shows a match in it.</summary>
   public void DrawAsFarUpAs(int shown)
   {
      for(var at = FirstDrawn - 1; at >= shown; at--) _drawn.Insert(0, Shown[at]);
   }

   /// <summary>Draws as many as <see cref="DrawnAtATime"/> more things above what is drawn, as Magnus scrolls up to its top.</summary>
   public void DrawMoreAbove()
   {
      var firstDrawn = FirstDrawn;
      for(var at = firstDrawn - 1; at >= Math.Max(0, firstDrawn - DrawnAtATime); at--) _drawn.Insert(0, Shown[at]);
   }
}
