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

   public When_a_chat_shows_a_long_conversation()
   {
      var session = TheSampleSession.RepeatedInTheSandbox(TimesOver);
      _window = Open(() => new Window { Width = 840, Height = 900, Content = new ChatOfASession(Sandbox.Started.HubOfTheSandbox.Hub) });
      _chat = OnTheUIThread(() => (ChatOfASession)_window.Content!);
      OnTheUIThread(() => _chat.Show(session));
      WaitUntil(() => _chat.Shown.Count > 0 && IsAtTheEnd, "the chat showed the end of the conversation");
      var drawnAtFirst = OnTheUIThread(() => Views.ToHashSet());
      for(var frame = 0; frame < FramesLeftAlone; frame++)
         OnTheUIThread(() =>
         {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
         });
      _viewsDrawnAnewWhileLeftAlone = OnTheUIThread(() => Views.Count(it => !drawnAtFirst.Contains(it)));
      _showsTheEndOnceLeftAlone = OnTheUIThread(() => IsAtTheEnd);
      Render(_window, "a-long-conversation");
   }

   private protected ScrollViewer TheScroller => _chat.FindDescendantOfType<ScrollViewer>()!;

   bool IsAtTheEnd => TheScroller.Offset.Y >= TheScroller.Extent.Height - TheScroller.Viewport.Height - 4;

   /// <summary>The views of what the chat draws.</summary>
   IEnumerable<Control> Views => _chat.FindDescendantOfType<ItemsControl>()!.GetRealizedContainers().OfType<ContentPresenter>().Select(it => it.Child!);

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

      public and_Magnus_scrolls_up_to_the_top_of_what_it_draws()
      {
         _firstDrawnBefore = OnTheUIThread(() => _chat.Drawn[0]);
         var whereTheFirstWas = 0.0;
         OnTheUIThread(() => TheScroller.ScrollChanged += (_, _) => { if(_chat.Drawn.Count == 100) whereTheFirstWas = WhereInViewIs(_firstDrawnBefore); });
         WaitUntil(() =>
         {
            if(_chat.Drawn.Count > 100) return true;
            _window.MouseWheel(new Point(420, 450), new Vector(0, 10), RawInputModifiers.None);
            return false;
         }, "Magnus had scrolled up to the top of what the chat drew with the mouse wheel");
         _whereTheFirstWas = whereTheFirstWas;
         Render(_window, "a-long-conversation-scrolled-to-the-top-of-what-it-drew");
      }

      /// <summary>Where in view <paramref name="shown"/> is drawn: where it is in the column, less how far the column is scrolled.</summary>
      double WhereInViewIs(ShownInTheChat shown)
      {
         var column = _chat.FindDescendantOfType<ItemsControl>()!;
         return column.ContainerFromItem(shown)!.TranslatePoint(default, column)!.Value.Y - TheScroller.Offset.Y;
      }

      [XF] public void it_draws_a_hundred_more_above() => OnTheUIThread(() => _chat.Drawn.SequenceEqual(_chat.Shown.TakeLast(200))).Must().BeTrue();

      [XF] public void what_was_drawn_first_stays_where_it_was_in_view() => OnTheUIThread(() => WhereInViewIs(_firstDrawnBefore)).Must().Satisfy(it => Math.Abs(it - _whereTheFirstWas) < 2);

      [XF] public void it_no_longer_follows_the_end() => OnTheUIThread(() => _chat.FollowsTheEnd).Must().BeFalse();
   }
}
