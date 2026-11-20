using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Controls.Documents;
using Avalonia.VisualTree;
using Cloyd.Chat;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat;

/// <summary>
/// The first run of tool calls of the session the desktop app's pictures show, <c>Read 2 files, ran 2 commands, 1
/// failed</c>, opened as Magnus clicks its line, and the first command in it opened as he clicks its row: as the app's
/// pictures <c>05-group-opened.png</c> and <c>07-command-opened.png</c> show them.
/// </summary>
public class When_Magnus_opens_a_run_of_tool_calls_and_a_command_in_it : SpecificationOpeningWindows
{
   readonly ChatOfASession _chat;

   public When_Magnus_opens_a_run_of_tool_calls_and_a_command_in_it()
   {
      var session = TheSampleSession.InTheSandbox();
      var window = Open(() => new Window { Width = 840, Height = 1300, Content = new ChatOfASession(Sandbox.Started.HubOfTheSandbox.Hub) });
      _chat = OnTheUIThread(() => (ChatOfASession)window.Content!);
      OnTheUIThread(() => _chat.Show(session));
      WaitUntil(() => TheScroller.Offset.Y > 0, "the chat showed the conversation at its end");
      WaitUntil(() =>
      {
         window.MouseWheel(new Point(420, 650), new Vector(0, 10), RawInputModifiers.None);
         return LinesThatOpen.Any(it => it.StartsWith("Read 2 files", StringComparison.Ordinal));
      }, "Magnus had scrolled up to the conversation's start with the mouse wheel");
      Click(LineThatOpens("Read 2 files, ran 2 commands, 1 failed"));
      WaitUntil(() => LinesThatOpen.Count > 2, "the run opened");
      Click(LineThatOpens("Ran ls..."));
      Render(window, "a-run-of-tool-calls-and-a-command-in-it-opened");
   }

   ScrollViewer TheScroller => OnTheUIThread(() => _chat.FindDescendantOfType<ScrollViewer>()!);

   /// <summary>The text of each line that opens what is under it, as the chat shows them now: in no particular order, as the virtualizing list keeps its views.</summary>
   List<string> LinesThatOpen => OnTheUIThread(() => Lines.Select(TextOf).ToList());

   Button LineThatOpens(string beginning) => OnTheUIThread(() => Lines.First(it => TextOf(it).StartsWith(beginning, StringComparison.Ordinal)));

   /// <summary>The lines that open what is under them: the buttons whose content is a line of text, not the buttons of a code block.</summary>
   IEnumerable<Button> Lines => _chat.GetVisualDescendants().OfType<Button>().Where(it => it.Content is TextBlock);

   static string TextOf(Button line) => ((TextBlock)line.Content!).Inlines!.Text?.Trim() ?? "";

   [XF] public void it_no_longer_follows_the_end_of_the_conversation() => OnTheUIThread(() => _chat.FollowsTheEnd).Must().BeFalse();

   [XF] public void it_shows_a_row_for_each_call_of_the_run_with_how_it_ended() =>
      LinesThatOpen.Must().Satisfy(it => new[] { "Read README.md  ›", "Reading NOTES.md  Failed  ›", "Ran ls...  ⌄", "Ran cat test_inventory.py...  ›" }.All(it.Contains));

   [XF] public void the_commands_row_shows_the_command_behind_a_dollar_sign() =>
      OnTheUIThread(() => _chat.GetVisualDescendants().OfType<SelectableTextBlock>().Any(it => it.Text?.StartsWith("$ cd \"C:/Users/magnu/AppData/Local/Temp/desktop-app-exploration/sample-shop\" && ls") == true)).Must().BeTrue();

   [XF] public void the_commands_row_shows_its_output() =>
      OnTheUIThread(() => _chat.GetVisualDescendants().OfType<SelectableTextBlock>().Any(it => it.Text?.StartsWith("README.md\n__pycache__\ninventory.py") == true)).Must().BeTrue();
}
