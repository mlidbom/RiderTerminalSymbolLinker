using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Cloyd.Projects;
using Cloyd.Sessions;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications;

public class When_Magnus_drags_a_session_tab : SpecificationOpeningWindows
{
   readonly SandboxProject _project;
   readonly ProjectWindow _window;
   readonly SessionTab _first, _second, _third;

   public When_Magnus_drags_a_session_tab()
   {
      _project = Sandbox.Project("reordering-tabs");
      _window = Open(_project.Window);
      WaitUntil(() => _window.BringingBackTheTabs.IsCompleted, "the project window opened");
      foreach(var name in new[] { "First session", "Second session", "Third session" })
         DoneOnTheUIThread(() => _window.StartANewSession(new NewSession(AgentCli.Codex, name)));
      var tabs = OnTheUIThread(() => _window.Tabs);
      (_first, _second, _third) = (tabs[0], tabs[1], tabs[2]);
      WaitUntil(() => _third.CodexChat?.InputBox.CanSend == true, "the selected session was ready");
   }

   [XF] public void dropping_before_the_first_tab_preserves_the_session_and_draft_and_the_order_on_reopen()
   {
      WaitUntilTheInputBoxHasTheKeyboard(_window, _third);
      Type(_window, "Keep this draft");
      OnTheUIThread(() =>
      {
         BeginDrag(_third, HeaderPoint(_first, 0.25));
         var mark = (Border)AdornerLayer.GetAdorner(_first)!;
         mark.Background.Must().Satisfy(it => it is ISolidColorBrush);
         mark.Bounds.Width.Must().Satisfy(it => it > 0);
         _window.Tabs.Must().SequenceEqual([_first, _second, _third]);
         Render(_window, "dragging-a-session-tab-before-the-first");
         _window.MouseUp(HeaderPoint(_first, 0.25), MouseButton.Left);
         _window.Tabs.Must().SequenceEqual([_third, _first, _second]);
         _window.SelectedTab.Must().Be(_third);
         _window.FindDescendantOfType<SessionTabStrip>()!.SelectedContent.Must().Be(_third.Content);
         _third.CodexChat!.InputBox.Draft.Document.Text.Must().Be("Keep this draft");
         AdornerLayer.GetAdorner(_first).Must().BeNull();
      });
      _project.ItsSessionHosts().Count.Must().Be(3);
      Close(_window);
      WaitUntil(() => _project.ItsSessionHosts().All(it => !it.Attached), "the three sessions were ready to reattach");
      var reopened = Open(_project.Window);
      WaitUntil(() => reopened.BringingBackTheTabs.IsCompleted && reopened.Tabs.Count == 3, "the three tabs reopened");
      OnTheUIThread(() => reopened.Tabs.Select(it => it.Host.Id).ToArray()).Must().SequenceEqual([_third.Host.Id, _first.Host.Id, _second.Host.Id]);
      OnTheUIThread(() => reopened.SelectedTab?.Host.Id).Must().Be(_third.Host.Id);
      WaitUntil(() => reopened.SelectedTab?.CodexChat?.InputBox.Draft.Document.Text == "Keep this draft", "the reordered session's draft came back");
   }

   [XF] public void a_drag_can_cross_a_wrapped_row_and_drop_after_its_last_tab()
   {
      OnTheUIThread(() => _window.Width = 350);
      WaitUntil(() => HeaderPoint(_third).Y > HeaderPoint(_first).Y, "the tab strip wrapped");
      OnTheUIThread(() =>
      {
         var after = HeaderPoint(_third, .9);
         BeginDrag(_first, after);
         _window.MouseUp(after, MouseButton.Left);
         _window.Tabs.Must().SequenceEqual([_second, _third, _first]);
         _window.SelectedTab.Must().Be(_first);
      });
      Press(_window, Key.Tab, PhysicalKey.Tab, RawInputModifiers.Control);
      OnTheUIThread(() => _window.SelectedTab).Must().Be(_second);
   }

   [XF] public void Escape_or_releasing_in_the_transcript_cancels_the_move()
   {
      OnTheUIThread(() => BeginDrag(_third, HeaderPoint(_first, .25)));
      Press(_window, Key.Escape, PhysicalKey.Escape, RawInputModifiers.None);
      OnTheUIThread(() =>
      {
         AdornerLayer.GetAdorner(_first).Must().BeNull();
         _window.MouseUp(HeaderPoint(_first, .25), MouseButton.Left);
         var transcript = new Point(180, 200);
         BeginDrag(_third, HeaderPoint(_first, .25));
         _window.MouseMove(transcript, RawInputModifiers.LeftMouseButton);
         AdornerLayer.GetAdorner(_first).Must().BeNull();
         _window.MouseUp(transcript, MouseButton.Left);
         _window.Tabs.Must().SequenceEqual([_first, _second, _third]);
      });
   }

   [XF] public void a_small_movement_is_a_click_and_the_close_button_still_ends_only_its_own_session()
   {
      OnTheUIThread(() =>
      {
         var point = HeaderPoint(_second);
         _window.MouseDown(point, MouseButton.Left);
         _window.MouseMove(point + new Vector(1, 0), RawInputModifiers.LeftMouseButton);
         _window.MouseUp(point + new Vector(1, 0), MouseButton.Left);
         _window.SelectedTab.Must().Be(_second);
         _window.Tabs.Must().SequenceEqual([_first, _second, _third]);
         var close = ((Control)_second.Header!).GetVisualDescendants().OfType<Button>().Single();
         var centre = close.TranslatePoint(new Point(close.Bounds.Width / 2, close.Bounds.Height / 2), _window)!.Value;
         _window.MouseDown(centre, MouseButton.Left);
         _window.MouseUp(centre, MouseButton.Left);
      });
      WaitUntil(() => _window.Tabs.Count == 2 && _project.SessionHost(_second.Host.Id) is null, "the close button ended its session");
      OnTheUIThread(() => _window.Tabs).Must().SequenceEqual([_first, _third]);
   }

   [XF] public void losing_pointer_capture_clears_the_marker_and_leaves_tabs_in_place() => OnTheUIThread(() =>
   {
      IPointer? pointer = null;
      _third.AddHandler(InputElement.PointerPressedEvent, (_, pressed) => pointer = pressed.Pointer, handledEventsToo: true);
      var target = HeaderPoint(_first, .25);
      BeginDrag(_third, target);
      pointer!.Capture(_window);
      AdornerLayer.GetAdorner(_first).Must().BeNull();
      _window.MouseUp(target, MouseButton.Left);
      _window.Tabs.Must().SequenceEqual([_first, _second, _third]);
   });

   [XF] public void releasing_the_left_button_while_holding_the_right_cancels_the_drag() => OnTheUIThread(() =>
   {
      var target = HeaderPoint(_first, .25);
      BeginDrag(_third, target);
      _window.MouseDown(target, MouseButton.Right, RawInputModifiers.LeftMouseButton);
      _window.MouseUp(target, MouseButton.Left, RawInputModifiers.RightMouseButton);
      _window.MouseMove(target + new Vector(2, 0), RawInputModifiers.RightMouseButton);
      AdornerLayer.GetAdorner(_first).Must().BeNull();
      _window.MouseUp(target, MouseButton.Right);
      _window.Tabs.Must().SequenceEqual([_first, _second, _third]);
   });

   void BeginDrag(SessionTab tab, Point target)
   {
      _window.MouseDown(HeaderPoint(tab), MouseButton.Left);
      _window.MouseMove(target, RawInputModifiers.LeftMouseButton);
      _window.UpdateLayout();
   }

   Point HeaderPoint(SessionTab tab, double across = .35) => tab.TranslatePoint(new Point(tab.Bounds.Width * across, tab.Bounds.Height / 2), _window)!.Value;
}
