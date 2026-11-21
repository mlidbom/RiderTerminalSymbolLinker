using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Cloyd.Chat;
using Cloyd.Chat.Searching;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;
using LiveMarkdown.Avalonia;

namespace Cloyd.Specifications.Chat.Searching;

/// <summary>
/// Magnus searches a chat's conversation with Ctrl+F, as in a browser, for "Started.", which is twice in the sample
/// session: in Magnus's prompt that asked Claude to reply it, and in Claude's reply. In the sample session's conversation
/// 8 times over, 160 things shown, the first 60 of them above what the chat draws at first: 16 matches, numbered down
/// the conversation. The chat shows its end, where the last time over's prompt is the first match in view. Magnus,
/// 2026-11-21: "How hard is a search in the transcript to write? If easy, please do it right away using standard search
/// keyboard shortcuts. Ctrl+F F3 etc". Drawn to <c>RenderedWindows/a-search-of-a-chat.png</c>.
/// </summary>
public class When_Magnus_searches_a_chat : SpecificationOpeningWindows
{
   const int TimesOver = 8;

   const string SearchedFor = "started.";

   private protected readonly Window _window;
   private protected readonly ChatOfASession _chat;

   public When_Magnus_searches_a_chat() => (_window, _chat) = Once(() =>
   {
      var hub = Sandbox.HubOfTheSandbox;
      var session = TheSampleSession.RepeatedFor(hub, TimesOver);
      var window = Open(() => new Window { Width = 840, Height = 900, Content = new ChatOfASession(hub.Hub, Sandbox.CloydsFolder.PastedImages) });
      var chat = OnTheUIThread(() => (ChatOfASession)window.Content!);
      OnTheUIThread(() => chat.Show(session));
      WaitUntil(() => chat.Shown.Count == TimesOver * 20, "the chat showed the whole conversation");
      OnTheUIThread(chat.TakeTheKeyboard);
      Press(window, Key.F, PhysicalKey.F, RawInputModifiers.Control);
      WaitUntil(() => chat.FindBar.HasTheKeyboard, "the find bar had the keyboard");
      Type(window, SearchedFor);
      WaitUntil(() => chat.MatchShown is not null, "the chat showed a match");
      Render(window, "a-search-of-a-chat");
      return (window, chat);
   });

   [XF] public void Ctrl_F_opens_the_find_bar_with_the_keyboard_in_its_box() => OnTheUIThread(() => _chat.FindBar is { IsOpen: true, HasTheKeyboard: true }).Must().BeTrue();

   /// <summary>As a browser shows the first match from the top of the page in view.</summary>
   [XF] public void it_counts_the_matches_and_shows_the_first_in_view() => OnTheUIThread(() => _chat.FindBar.Counted).Must().Be("15 of 16");

   [XF] public void it_paints_the_match_shown_whatever_its_case() => OnTheUIThread(() => TextOf(_chat.MatchShown!.Value)).Must().Be("Started.");

   [XF] public void the_match_shown_is_in_Magnuss_last_prompt() => OnTheUIThread(() => IsInTheViewOf(PromptsThatSayIt()[^1], _chat.MatchShown!.Value.Block)).Must().BeTrue();

   [XF] public void the_match_shown_is_in_view() => OnTheUIThread(() => IsInView(_chat.MatchShown!.Value)).Must().BeTrue();

   /// <summary>Those of the last 5 times over, the 100 things the chat draws: in Magnus's prompts' bubbles and in Claude's markdown.</summary>
   [XF] public void it_paints_each_match_drawn() => OnTheUIThread(() => BlocksPainted(MatchesPainted.AMatch)).Must().Be(10);

   /// <summary>Then Magnus presses F3 twice: down to Claude's last reply, then round to the match furthest up, in his first prompt, which the chat didn't draw.</summary>
   public class and_then_presses_F3_twice : When_Magnus_searches_a_chat
   {
      public and_then_presses_F3_twice() => Once(() =>
      {
         Press(_window, Key.F3, PhysicalKey.F3, RawInputModifiers.None);
         Press(_window, Key.F3, PhysicalKey.F3, RawInputModifiers.None);
         WaitUntil(() => _chat.FindBar.Counted == "1 of 16" && _chat.MatchShown is not null, "the chat showed the first match", () => $"it said {_chat.FindBar.Counted}");
         Render(_window, "a-search-of-a-chat-gone-round");
         return true;
      });

      [XF] public void it_draws_the_conversation_up_to_it() => OnTheUIThread(() => _chat.Drawn.Count).Must().Satisfy(it => it > 140);

      [XF] public void the_match_shown_is_in_Magnuss_first_prompt() => OnTheUIThread(() => IsInTheViewOf(PromptsThatSayIt()[0], _chat.MatchShown!.Value.Block)).Must().BeTrue();

      [XF] public void it_scrolls_it_into_view() => OnTheUIThread(() => IsInView(_chat.MatchShown!.Value)).Must().BeTrue();

      [XF] public void one_match_is_painted_as_the_one_shown() => OnTheUIThread(() => BlocksPainted(MatchesPainted.TheMatchShown)).Must().Be(1);
   }

   /// <summary>
   /// Then Magnus presses Shift+F3, up to Claude's reply the time over before, out of view: a match in Claude's markdown,
   /// which the chat once failed to scroll to, its text block's request to be brought into view lost.
   /// </summary>
   public class and_then_presses_Shift_F3 : When_Magnus_searches_a_chat
   {
      public and_then_presses_Shift_F3() => Once(() =>
      {
         Press(_window, Key.F3, PhysicalKey.F3, RawInputModifiers.Shift);
         WaitUntil(() => _chat.FindBar.Counted == "14 of 16" && _chat.MatchShown is not null, "the chat showed the match above", () => $"it said {_chat.FindBar.Counted}");
         return true;
      });

      [XF] public void the_match_shown_is_in_Claudes_reply_the_time_over_before() => OnTheUIThread(() => IsInTheViewOf(RepliesThatSayIt()[^2], _chat.MatchShown!.Value.Block)).Must().BeTrue();

      [XF] public void it_scrolls_it_into_view() => OnTheUIThread(() => IsInView(_chat.MatchShown!.Value)).Must().BeTrue();
   }
   /// <summary>Then Magnus presses Escape.</summary>
   public class and_then_presses_Escape : When_Magnus_searches_a_chat
   {
      public and_then_presses_Escape() => Once(() =>
      {
         Press(_window, Key.Escape, PhysicalKey.Escape, RawInputModifiers.None);
         WaitUntil(() => !_chat.FindBar.IsOpen && _chat.InputBox.IsKeyboardFocusWithin, "the find bar closed, and the input box had the keyboard");
         return true;
      });

      [XF] public void no_match_is_painted() => OnTheUIThread(() => BlocksPainted(MatchesPainted.AMatch) + BlocksPainted(MatchesPainted.TheMatchShown)).Must().Be(0);

      [XF] public void the_keyboard_goes_back_to_the_input_box() => OnTheUIThread(() => _chat.InputBox.IsKeyboardFocusWithin).Must().BeTrue();
   }

   int BlocksPainted(string highlight) => _chat.GetVisualDescendants().OfType<MarkdownTextBlock>().Count(it => it.Highlights.TryGetValue(highlight, out _));

   bool IsInView(TextHighlightMatch match)
   {
      var scroller = _chat.FindDescendantOfType<ScrollViewer>()!;
      var bounds = match.Block.GetTextRangeBoundsInControl(match.Range.Start, match.Range.Length)[0];
      var top = match.Block.TranslatePoint(bounds.TopLeft, scroller)!.Value.Y;
      return top >= 0 && top + bounds.Height <= scroller.Viewport.Height;
   }

   /// <summary>Whether <paramref name="block"/> is in the view the chat drew of <paramref name="shown"/>.</summary>
   bool IsInTheViewOf(ShownInTheChat shown, MarkdownTextBlock block) => _chat.Drawn.ToList().IndexOf(shown) is var at and >= 0 && block.FindAncestorOfType<ItemsControl>() is {} column && column.ContainerFromIndex(at)?.IsVisualAncestorOf(block) == true;

   List<ShownInTheChat.Prompt> PromptsThatSayIt() => [.. _chat.Shown.OfType<ShownInTheChat.Prompt>().Where(it => it.Text.Contains("Started.", StringComparison.Ordinal))];

   List<ShownInTheChat.ClaudesText> RepliesThatSayIt() => [.. _chat.Shown.OfType<ShownInTheChat.ClaudesText>().Where(it => it.Markdown == "Started.")];

   static string TextOf(TextHighlightMatch match) => match.Block.LayoutText.Substring(match.Range.Start, match.Range.Length);
}
