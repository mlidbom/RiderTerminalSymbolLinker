using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cloyd.Codex;
using Cloyd.Codex.Chat;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat;

public class When_Magnus_inspects_the_bottom_of_a_chat : SpecificationOpeningWindows
{
   readonly Window _window;
   readonly ChatOfACodexSession _chat;
   readonly ScrollViewer _scroller;

   public When_Magnus_inspects_the_bottom_of_a_chat()
   {
      var sandbox = Sandbox.HubOfTheSandbox;
      var session = new CodexSessionId(Guid.NewGuid().ToString());
      var lines = string.Join('\n', Enumerable.Range(1, 70).Select(it => $"Line {it}"));
      var prompts = new[] { lines, $"<peer-message from=\"cloyd-gpt\" from_kind=\"codex\">\n{lines}\n</peer-message>" }
                   .Select((text, index) => new { id = $"prompt-{index}", type = "userMessage", content = new[] { new { type = "text", text } } });
      var items = JsonSerializer.SerializeToNode(prompts)!.AsArray();
      foreach(var number in Enumerable.Range(1, 24))
         items.Add(JsonSerializer.SerializeToNode(new { id = $"command-{number}", type = "commandExecution", command = $"Get-Content report-{number}.txt", aggregatedOutput = lines, status = "completed", exitCode = 0 }));
      items.Add(JsonSerializer.SerializeToNode(new { id = "tail", type = "userMessage", content = new[] { new { type = "text", text = string.Join('\n', Enumerable.Repeat("A following row", 8)) } } }));
      new CodexSessionOfASpecification(session, Sandbox.Project("reading-the-bottom").Project.Folder, "scrolling")
         { RecordedItems = items }.WriteItsRecordFor(sandbox);
      _window = Open(() => new Window { Width = 640, Height = 480, Content = new ChatOfACodexSession(sandbox.Hub, Sandbox.CloydsFolder.PastedImages, new CodexSessionDrafts()) });
      _chat = OnTheUIThread(() => (ChatOfACodexSession)_window.Content!);
      _scroller = OnTheUIThread(() => _chat.FindDescendantOfType<ScrollViewer>()!);
      OnTheUIThread(() => { _scroller.AllowAutoHide = false; _chat.Show(session, null); });
      WaitUntil(() => _scroller.Offset.Y > 0 && AtBottom, "the transcript opened at its end");
   }

   [XF] public void opening_a_peer_message_at_the_bottom_keeps_its_header_where_it_was_clicked() =>
      CheckOpening(it => AutomationProperties.GetName(it) == "Message from cloyd-gpt, Peers");

   [XF] public void opening_a_nested_tool_call_keeps_its_heading_in_place_above_the_following_message()
   {
      OnTheUIThread(() =>
      {
         var group = _chat.GetVisualDescendants().OfType<Button>().Single(it => it.Content is TextBlock label && label.Inlines?.Text?.Contains("24 tool calls") == true);
         Click(group);
         Layout();
         _window.MouseWheel(CentreInWindow(_scroller), new Vector(0, -1000), RawInputModifiers.None);
      });
      WaitUntil(() => AtBottom && _chat.FollowsTheEnd, "Magnus scrolled to the last command and the following message");
      CheckOpening(it => it.Content is TextBlock label && label.Inlines?.Text?.Contains("Get-Content report-24.txt") == true);
   }

   [XF] public void losing_pointer_capture_at_the_bottom_does_not_resume_following() => OnTheUIThread(() =>
   {
      var thumb = VerticalTrack.Thumb!;
      IPointer? pointer = null;
      thumb.AddHandler(InputElement.PointerPressedEvent, (_, pressed) => pointer = pressed.Pointer, handledEventsToo: true);
      var point = CentreInWindow(thumb);
      _window.MouseDown(point, MouseButton.Left);
      pointer!.Capture(_window);
      Layout();
      _chat.FollowsTheEnd.Must().BeFalse();
      _window.MouseUp(point, MouseButton.Left);
   });

   void CheckOpening(Func<Button, bool> isHeader) => OnTheUIThread(() =>
   {
      var header = _chat.GetVisualDescendants().OfType<Button>().Single(isHeader);
      var before = CentreInWindow(header);
      var extentBefore = _scroller.Extent.Height;
      _window.MouseDown(before, MouseButton.Left);
      _window.MouseUp(before, MouseButton.Left);
      Layout();
      _scroller.Extent.Height.Must().Satisfy(it => it > extentBefore + _scroller.Viewport.Height);
      Math.Abs(CentreInWindow(header).Y - before.Y).Must().Satisfy(it => it < 2);
      _chat.FollowsTheEnd.Must().BeFalse();
   });

   [XF] public void one_held_drag_can_touch_the_bottom_and_reverse_then_releasing_at_the_end_resumes_following() => OnTheUIThread(() =>
   {
      var track = VerticalTrack;
      var thumb = track.Thumb!;
      var bottom = track.TranslatePoint(new Point(track.Bounds.Width / 2, track.Bounds.Height - 1), _window)!.Value;
      var middle = CentreInWindow(track);
      _window.MouseDown(CentreInWindow(thumb), MouseButton.Left);
      DragTo(middle);
      AtBottom.Must().BeFalse();
      DragTo(bottom);
      AtBottom.Must().BeTrue();
      DragTo(middle);
      AtBottom.Must().BeFalse();
      _window.MouseUp(middle, MouseButton.Left);
      Layout();
      _chat.FollowsTheEnd.Must().BeFalse();

      _window.MouseDown(CentreInWindow(thumb), MouseButton.Left);
      _chat.FollowsTheEnd.Must().BeFalse();
      _window.MouseMove(bottom, RawInputModifiers.LeftMouseButton);
      _window.MouseUp(bottom, MouseButton.Left);
      Layout();
      AtBottom.Must().BeTrue();
      _chat.FollowsTheEnd.Must().BeTrue();

      _window.MouseDown(CentreInWindow(thumb), MouseButton.Left);
      _window.MouseMove(middle, RawInputModifiers.LeftMouseButton);
      _window.MouseUp(middle, MouseButton.Left);
      Layout();
      AtBottom.Must().BeFalse();
      _chat.FollowsTheEnd.Must().BeFalse();

      void DragTo(Point point)
      {
         _window.MouseMove(point, RawInputModifiers.LeftMouseButton);
         Layout();
      }
   });

   bool AtBottom => _scroller.Extent.Height - _scroller.Viewport.Height - _scroller.Offset.Y <= 1;
   Track VerticalTrack => _scroller.GetVisualDescendants().OfType<ScrollBar>().Single(it => it.Orientation == Orientation.Vertical && it.TemplatedParent == _scroller)
                                   .FindDescendantOfType<Track>()!;
   Point CentreInWindow(Control control) => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), _window)!.Value;

   static void Layout()
   {
      AvaloniaHeadlessPlatform.ForceRenderTimerTick();
      Dispatcher.UIThread.RunJobs();
   }
}
