using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using AvaloniaEdit;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Cloyd.Chat;
using Cloyd.Chat.Searching;
using Cloyd.ClaudeCode;
using Cloyd.Codex;
using Cloyd.Codex.Chat;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;
using LiveMarkdown.Avalonia;

namespace Cloyd.Specifications.Chat;

public class When_Magnus_sends_markdown : SpecificationOpeningWindows
{
   const string Message = """
      Some **oddities** though:

      * Open tabs
        * Include the dashboard [Image #1]
      * [Open project](https://example.org/hidden-target)

      What do you think?
      """;

   [XF] public void Claude_and_Codex_show_his_nested_list_inside_the_prompt_bubble()
   {
      var hub = Sandbox.HubOfTheSandbox;
      var folder = Sandbox.Project("markdown-prompts").Project.Folder;
      var claude = new SessionOfASpecification(new ClaudeCodeSessionId(Guid.NewGuid().ToString()), folder, Message);
      var codex = new CodexSessionOfASpecification(new CodexSessionId(Guid.NewGuid().ToString()), folder, Message);
      claude.WriteItsTranscriptFor(hub);
      codex.WriteItsRecordFor(hub);
      var images = Sandbox.CloydsFolder.PastedImages;
      var chats = OnTheUIThread(() => new SessionChat[]
      {
         new ChatOfAClaudeCodeSession(hub.Hub, images),
         new ChatOfACodexSession(hub.Hub, images, new CodexSessionDrafts())
      });
      foreach(var chat in chats)
      {
         var window = Open(() => new Window { Width = 780, Height = 560, Content = chat });
         OnTheUIThread(() =>
         {
            if(chat is ChatOfAClaudeCodeSession claudeChat) claudeChat.Show(claude.Id);
            else ((ChatOfACodexSession)chat).Show(codex.Id, null);
         });
         WaitUntil(() => Text("Include the dashboard [Image #1]") is { Bounds.Height: > 0 }, "the indented list item and image reference were drawn in the prompt");
         OnTheUIThread(() =>
         {
            var nested = Text("Include the dashboard [Image #1]")!;
            var parent = Text("Open tabs")!;
            var sibling = Text("Open project")!;
            var x = parent.TranslatePoint(default, window)!.Value.X;
            (nested.TranslatePoint(default, window)!.Value.X > x).Must().BeTrue();
            sibling.TranslatePoint(default, window)!.Value.X.Must().Be(x);
            Text("Some oddities though:").Must().NotBeNull();
            var prompt = chat.Shown.OfType<ShownInTheChat.Prompt>().Single();
            prompt.Text.Must().Be(Message);
            FoundInTheChat.Of("Some oddities though", [prompt], 0).Counted.Must().Be("1 of 1");
            FoundInTheChat.Of("hidden-target", [prompt], 0).Counted.Must().Be("No matches");
         });
         if(chat is ChatOfACodexSession) Render(window, "Magnus-markdown-prompt");
         var editor = OnTheUIThread(() => chat.InputBox.GetVisualDescendants().OfType<TextEditor>().Single());
         OnTheUIThread(() =>
         {
            editor.Document.Text = "* **Preview this**";
            editor.Select(4, 7);
            chat.InputBox.TakeTheKeyboard();
            window.KeyPress(Key.M, RawInputModifiers.Control, PhysicalKey.M, "m");
         });
         WaitUntil(() => chat.InputBox.GetVisualDescendants().OfType<MarkdownTextBlock>().Any(it => it.LayoutText == "Preview this"),
                   "holding Ctrl+M rendered the draft inside the composer");
         OnTheUIThread(() =>
         {
            window.KeyRelease(Key.LeftCtrl, RawInputModifiers.None, PhysicalKey.ControlLeft, null);
            editor.Opacity.Must().Be(1.0);
            window.KeyPress(Key.M, RawInputModifiers.None, PhysicalKey.M, "m");
            window.KeyTextInput("m");
            window.KeyRelease(Key.M, RawInputModifiers.None, PhysicalKey.M, "m");
            editor.IsKeyboardFocusWithin.Must().BeTrue();
            editor.SelectedText.Must().Be("Preview");
            editor.Document.Text.Must().Be("* **Preview this**");
            window.KeyPress(Key.M, RawInputModifiers.Control, PhysicalKey.M, "m");
            window.KeyRelease(Key.M, RawInputModifiers.Control, PhysicalKey.M, "m");
            editor.Opacity.Must().Be(1.0);
            window.KeyTextInput("m");
            editor.Document.Text.Must().Be("* **m this**");
         });
         DoneOnTheUIThread(async () =>
         {
            var renderer = Text("Open tabs")!.FindAncestorOfType<MarkdownRenderer>()!;
            renderer.SelectAll();
            var selection = renderer.SelectedText;
            selection.Must().Contain("Include the dashboard [Image #1]");
            renderer.Copy();
            (await window.Clipboard!.TryGetTextAsync()).Must().Be(selection);
            renderer.ClearSelection();
         });

         MarkdownTextBlock? Text(string text) => chat.GetVisualDescendants().OfType<Border>()
            .Where(it => it.HorizontalAlignment == HorizontalAlignment.Right && it.Child is MarkdownRenderer)
            .SelectMany(it => it.GetVisualDescendants().OfType<MarkdownTextBlock>()).FirstOrDefault(it => it.LayoutText == text);
      }
   }
}
