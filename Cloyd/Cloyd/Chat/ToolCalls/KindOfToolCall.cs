namespace Cloyd.Chat.ToolCalls;

/// <summary>
/// What a tool call does, as the chat words it: its row's verb in the past tense once done, such as <c>Read</c>, and in the
/// present tense until then, such as <c>Reading</c>; and how the line of a run of calls counts calls of the kind, such as
/// <c>read 2 files</c>.
/// </summary>
sealed class KindOfToolCall
{
   readonly string _countedOne;
   readonly string _countedSeveral;

   KindOfToolCall(string pastTense, string presentTense, string countedOne, string countedSeveral)
   {
      PastTense = pastTense;
      PresentTense = presentTense;
      _countedOne = countedOne;
      _countedSeveral = countedSeveral;
   }

   public static readonly KindOfToolCall Reading = new("Read", "Reading", "read a file", "read {0} files");
   public static readonly KindOfToolCall Editing = new("Edited", "Editing", "edited a file", "edited {0} files");
   public static readonly KindOfToolCall Creating = new("Created", "Creating", "created a file", "created {0} files");
   public static readonly KindOfToolCall Overwriting = new("Wrote", "Writing", "wrote a file", "wrote {0} files");
   public static readonly KindOfToolCall Running = new("Ran", "Running", "ran a command", "ran {0} commands");
   public static readonly KindOfToolCall Searching = new("Searched for", "Searching for", "searched for a pattern", "searched for {0} patterns");
   public static readonly KindOfToolCall Fetching = new("Fetched", "Fetching", "fetched a page", "fetched {0} pages");
   public static readonly KindOfToolCall SearchingTheWeb = new("Searched the web for", "Searching the web for", "searched the web", "searched the web {0} times");
   public static readonly KindOfToolCall Delegating = new("Delegated", "Delegating", "delegated a task", "delegated {0} tasks");
   public static readonly KindOfToolCall Calling = new("Called", "Calling", "called a tool", "called {0} tools");

   /// <summary>The verb of a call of the kind once done, such as <c>Read</c>.</summary>
   public string PastTense { get; }

   /// <summary>The verb of a call of the kind until it is done, or that failed or was stopped, such as <c>Reading</c>.</summary>
   public string PresentTense { get; }

   /// <summary>How the line of a run counts <paramref name="calls"/> of the kind, such as <c>read 2 files</c>.</summary>
   public string Counted(int calls) => calls == 1 ? _countedOne : string.Format(_countedSeveral, calls);
}
