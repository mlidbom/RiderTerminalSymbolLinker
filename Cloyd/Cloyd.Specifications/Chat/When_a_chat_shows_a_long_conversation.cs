using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cloyd.Chat;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat;

/// <summary>
/// A chat showing a conversation as long as Magnus's are: the sample session's a hundred times over, 2,000 things shown.
/// Once it shows the end, it is left alone for a while, as Magnus reads it, and drawn as it is then:
/// <c>RenderedWindows/a-long-conversation.png</c>.
/// </summary>
/// <remarks>
/// Drawn as a virtualizing list, such a chat flickered without end, and Cloyd stopped answering: see
/// <see cref="DrawnEndOfTheConversation"/>.
/// </remarks>
public class When_a_chat_shows_a_long_conversation : SpecificationOpeningWindows
{
   const int TimesOver = 100;

   /// <summary>How many times it is drawn while it is left alone: as many as in a second and a half at 60 frames a second.</summary>
   const int FramesLeftAlone = 90;

   private protected readonly Window _window;
   private protected readonly ChatOfASession _chat;
   readonly int _viewsDrawnAnewWhileLeftAlone;
   readonly bool _showsTheEndOnceLeftAlone;

   public When_a_chat_shows_a_long_conversation() => (_window, _chat, _viewsDrawnAnewWhileLeftAlone, _showsTheEndOnceLeftAlone) = Once(() =>
   {
      var hub = Sandbox.HubOfTheSandbox;
      var session = TheSampleSession.RepeatedFor(hub, TimesOver);
      var window = Open(() => new Window { Width = 840, Height = 900, Content = new ChatOfASession(hub.Hub) });
      var chat = OnTheUIThread(() => (ChatOfASession)window.Content!);
      OnTheUIThread(() => chat.Show(session));
      WaitUntil(() => chat.Shown.Count > 0 && IsAtTheEnd(chat), "the chat showed the end of the conversation");
      var drawnAtFirst = OnTheUIThread(() => ViewsOf(chat).ToHashSet());
      for(var frame = 0; frame < FramesLeftAlone; frame++)
         OnTheUIThread(() =>
         {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
         });
      var viewsDrawnAnewWhileLeftAlone = OnTheUIThread(() => ViewsOf(chat).Count(it => !drawnAtFirst.Contains(it)));
      var showsTheEndOnceLeftAlone = OnTheUIThread(() => IsAtTheEnd(chat));
      Render(window, "a-long-conversation");
      return (window, chat, viewsDrawnAnewWhileLeftAlone, showsTheEndOnceLeftAlone);
   });

   private protected static ScrollViewer TheScrollerOf(ChatOfASession chat) => chat.FindDescendantOfType<ScrollViewer>()!;

   static bool IsAtTheEnd(ChatOfASession chat) => TheScrollerOf(chat).Offset.Y >= TheScrollerOf(chat).Extent.Height - TheScrollerOf(chat).Viewport.Height - 4;

   /// <summary>The views of what <paramref name="chat"/> draws.</summary>
   static IEnumerable<Control> ViewsOf(ChatOfASession chat) => chat.FindDescendantOfType<ItemsControl>()!.GetRealizedContainers().OfType<ContentPresenter>().Select(it => it.Child!);

   [XF] public void it_shows_all_of_it() => OnTheUIThread(() => _chat.Shown.Count).Must().Be(20 * TimesOver);

   [XF] public void it_draws_the_last_hundred_things() => OnTheUIThread(() => _chat.Drawn.SequenceEqual(_chat.Shown.TakeLast(100))).Must().BeTrue();

   [XF] public void it_still_shows_the_end_once_left_alone() => _showsTheEndOnceLeftAlone.Must().BeTrue();

   [XF] public void it_draws_nothing_anew_while_left_alone() => _viewsDrawnAnewWhileLeftAlone.Must().Be(0);

   [XF] public void it_still_follows_the_end() => OnTheUIThread(() => _chat.FollowsTheEnd).Must().BeTrue();

   public class and_Magnus_scrolls_up_to_the_top_of_what_it_draws : When_a_chat_shows_a_long_conversation
   {
      readonly ShownInTheChat _firstDrawnBefore;

      /// <summary>Where in view the first thing drawn was as the chat began to draw more above it, in logical pixels from the top of the view.</summary>
      readonly double _whereTheFirstWas;

      public and_Magnus_scrolls_up_to_the_top_of_what_it_draws() => (_firstDrawnBefore, _whereTheFirstWas) = Once(() =>
      {
         var firstDrawnBefore = OnTheUIThread(() => _chat.Drawn[0]);
         var whereTheFirstWas = 0.0;
         OnTheUIThread(() => TheScrollerOf(_chat).ScrollChanged += (_, _) => { if(_chat.Drawn.Count == 100) whereTheFirstWas = WhereInViewIs(firstDrawnBefore); });
         WaitUntil(() =>
         {
            if(_chat.Drawn.Count > 100) return true;
            _window.MouseWheel(new Point(420, 450), new Vector(0, 10), RawInputModifiers.None);
            return false;
         }, "Magnus had scrolled up to the top of what the chat drew with the mouse wheel");
         Render(_window, "a-long-conversation-scrolled-to-the-top-of-what-it-drew");
         return (firstDrawnBefore, whereTheFirstWas);
      });

      /// <summary>Where in view <paramref name="shown"/> is drawn: where it is in the column, less how far the column is scrolled.</summary>
      double WhereInViewIs(ShownInTheChat shown)
      {
         var column = _chat.FindDescendantOfType<ItemsControl>()!;
         return column.ContainerFromItem(shown)!.TranslatePoint(default, column)!.Value.Y - TheScrollerOf(_chat).Offset.Y;
      }

      [XF] public void it_draws_a_hundred_more_above() => OnTheUIThread(() => _chat.Drawn.SequenceEqual(_chat.Shown.TakeLast(200))).Must().BeTrue();

      [XF] public void what_was_drawn_first_stays_where_it_was_in_view() => OnTheUIThread(() => WhereInViewIs(_firstDrawnBefore)).Must().Satisfy(it => Math.Abs(it - _whereTheFirstWas) < 2);

      [XF] public void it_no_longer_follows_the_end() => OnTheUIThread(() => _chat.FollowsTheEnd).Must().BeFalse();
   }
}
