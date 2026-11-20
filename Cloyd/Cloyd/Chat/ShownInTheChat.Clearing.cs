namespace Cloyd.Chat;

partial record ShownInTheChat
{
   /// <summary>
   /// <c>/clear</c> ended the session before it, and the conversation goes on in a new one: a grey line after the
   /// <c>/clear</c>'s bubble. Not seen in the app yet: Cloyd's own.
   /// </summary>
   /// <param name="Id">The message's id.</param>
   public sealed record Clearing(string Id) : ShownInTheChat
   {
      /// <summary>The line.</summary>
      public const string Line = "Cleared · a new session begins";
   }
}
