using Avalonia.Media;

namespace Cloyd.Chat.Views;

/// <summary>
/// The chat's colours: near those of the Claude desktop app's dark theme (<c>dev_docs/cloyd-chat-specification.md</c>,
/// picture <c>19-dark-theme.png</c>), as Cloyd's windows are dark.
/// </summary>
static class ColoursOfTheChat
{
   /// <summary>Claude's text, a prompt's, and what a tool call acted on.</summary>
   public static readonly IBrush Text = Brush("#E8E7E3");

   /// <summary>The grey lines: a run of tool calls counted, a call's verb, a compaction.</summary>
   public static readonly IBrush Grey = Brush("#9C9A95");

   /// <summary>The bubble of a prompt of Magnus's.</summary>
   public static readonly IBrush BubbleOfAPrompt = Brush("#30302E");

   /// <summary>A slash command in its bubble.</summary>
   public static readonly IBrush SlashCommand = Brush("#7FA8F5");

   /// <summary>A tool call that failed: its <c>Failed</c>.</summary>
   public static readonly IBrush Failed = Brush("#E5605A");

   /// <summary>The lines an edit put in: <c>+9</c>, and each such line of a change.</summary>
   public static readonly IBrush Added = Brush("#6CB46F");

   /// <summary>The lines an edit took out.</summary>
   public static readonly IBrush Removed = Brush("#E5605A");

   /// <summary>The ground of a line an edit put in.</summary>
   public static readonly IBrush GroundOfAnAddedLine = Brush("#1E3A22");

   /// <summary>The ground of a line an edit took out.</summary>
   public static readonly IBrush GroundOfARemovedLine = Brush("#3F1F1E");

   /// <summary>The thin border of an opened run of tool calls, a command, and the lines between the rows.</summary>
   public static readonly IBrush Border = Brush("#3A3936");

   static IBrush Brush(string colour) => new SolidColorBrush(Color.Parse(colour)).ToImmutable();
}
