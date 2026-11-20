namespace Cloyd.Chat.ToolCalls;

/// <summary>
/// What an edit changed in a file, line by line: the lines it took out and the lines it put in, and those it left between
/// them. Found as the longest run of lines both texts have in common, as a diff finds it.
/// </summary>
sealed class ChangeToAFile
{
   /// <summary>Above how many lines a text is compared as all taken out and all put in: a line-by-line comparison costs the product of the two counts.</summary>
   const int MostLinesCompared = 3000;

   ChangeToAFile(IReadOnlyList<LineOfAChange> lines) => Lines = lines;

   /// <summary>Each line, in order: those taken out before those put in where they replace each other.</summary>
   public IReadOnlyList<LineOfAChange> Lines { get; }

   /// <summary>How many lines it put in, as the chat shows in green, such as <c>+3</c>.</summary>
   public int LinesAdded => Lines.Count(it => it.Kind == WhatHappenedToTheLine.Added);

   /// <summary>The change that replacing <paramref name="before"/> with <paramref name="after"/> makes.</summary>
   public static ChangeToAFile From(string before, string after)
   {
      var removed = LinesOf(before);
      var added = LinesOf(after);
      if(removed.Length > MostLinesCompared || added.Length > MostLinesCompared)
         return new([.. removed.Select(it => new LineOfAChange(WhatHappenedToTheLine.Removed, it)), .. added.Select(it => new LineOfAChange(WhatHappenedToTheLine.Added, it))]);

      // In common[i, j]: how many lines the texts have in common from removed[i] and added[j] on.
      var common = new int[removed.Length + 1, added.Length + 1];
      for(var i = removed.Length - 1; i >= 0; i--)
         for(var j = added.Length - 1; j >= 0; j--)
            common[i, j] = removed[i] == added[j] ? common[i + 1, j + 1] + 1 : Math.Max(common[i + 1, j], common[i, j + 1]);

      var lines = new List<LineOfAChange>();
      int fromRemoved = 0, fromAdded = 0;
      while(fromRemoved < removed.Length || fromAdded < added.Length)
         if(fromRemoved < removed.Length && fromAdded < added.Length && removed[fromRemoved] == added[fromAdded])
         {
            lines.Add(new(WhatHappenedToTheLine.Unchanged, removed[fromRemoved++]));
            fromAdded++;
         }
         else if(fromAdded == added.Length || fromRemoved < removed.Length && common[fromRemoved + 1, fromAdded] >= common[fromRemoved, fromAdded + 1])
            lines.Add(new(WhatHappenedToTheLine.Removed, removed[fromRemoved++]));
         else
            lines.Add(new(WhatHappenedToTheLine.Added, added[fromAdded++]));
      return new(lines);
   }

   /// <summary>The change that writing <paramref name="content"/> as a new file makes: every line put in.</summary>
   public static ChangeToAFile Creating(string content) => From("", content);

   static string[] LinesOf(string text) => text.Length == 0 ? [] : text.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');
}
