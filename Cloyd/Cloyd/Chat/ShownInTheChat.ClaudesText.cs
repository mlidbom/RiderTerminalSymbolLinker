namespace Cloyd.Chat;

partial record ShownInTheChat
{
   /// <summary>What Claude wrote to Magnus, rendered as Markdown in a reply bubble.</summary>
   /// <param name="Id">The message's id.</param>
   /// <param name="Markdown">The text, as markdown.</param>
   public sealed record ClaudesText(string Id, string Markdown) : ShownInTheChat
   {
      /// <summary>Its markdown, which a search finds about as often in as in the text rendered of it.</summary>
      public override string TextShown => Markdown;
   }
}
