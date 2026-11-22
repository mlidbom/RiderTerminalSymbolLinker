using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cloyd.Chat.Writing;
using Cloyd.Sessions;
using Cloyd.Specifications.Scenarios;
using Cloyd.Specifications.TheSandbox;
using Compze.Threading;
using Compze.Threading.Interprocess;

namespace Cloyd.Specifications;

/// <summary>
/// A specification that opens Cloyd's windows on Avalonia's headless platform: laid out and drawn with Skia, on no
/// screen, on <see cref="TheHeadlessUIThread"/>, which every specification of the assembly shares. Each scenario has a
/// <see cref="TheSandbox.Sandbox"/> of its own. The windows it opens are closed, and its sandbox ended, when its scenario
/// ends: when the fact is done, or, built in <see cref="Once{TPart}"/>, after the last fact of its class.
/// </summary>
/// <remarks>
/// Modelled on JAStudio's and Deskmancer's <c>SpecificationOpeningWindows</c>.
/// </remarks>
public abstract class SpecificationOpeningWindows : IDisposable
{
   static readonly TimeSpan HowLongWaitingMayTake = TimeSpan.FromSeconds(30);

   readonly WhatAScenarioStarted _startedForThisFact = new();
   ScenarioOfAClass? _scenarioOfTheClass;
   bool _buildingTheScenarioOfTheClass;

   /// <summary>What the scenario being set up now showed and started: the class's while <see cref="Once{TPart}"/> builds it, else this fact's.</summary>
   WhatAScenarioStarted StartedNow => _buildingTheScenarioOfTheClass ? _scenarioOfTheClass!.Started : _startedForThisFact;

   /// <summary>
   /// The sandbox of the scenario this fact runs in: its class's, once <see cref="Once{TPart}"/> has built a part of it,
   /// else this fact's own. Started as it is first asked for, and ended as its scenario ends.
   /// </summary>
   private protected Sandbox Sandbox => (_scenarioOfTheClass?.Started ?? _startedForThisFact).Sandbox;

   /// <summary>The windows this fact's scenario, and its class's, showed and haven't closed.</summary>
   IEnumerable<Window> WindowsShown => (_scenarioOfTheClass?.Started.Windows ?? []).Concat(_startedForThisFact.Windows);

   /// <summary>
   /// What <paramref name="built"/> builds, as a part of the <see cref="ScenarioOfAClass"/> that all the facts of this
   /// specification's class share: built by the first, and kept for the others. The windows it shows, and what it has
   /// <see cref="EndWithTheScenario{TStarted}"/>, end after the class's last fact.
   /// </summary>
   /// <remarks>
   /// Only for a class whose facts only look at what the scenario built, as a fact that changes it would change it for the
   /// facts after it. A class whose base class builds its part with it builds its own with it too: else what its
   /// constructor does is done again for each fact, to the scenario they share.<br/><paramref name="file"/> and <paramref name="line"/>, the place in the code of the call, are filled in by the compiler.
   /// </remarks>
   protected TPart Once<TPart>(Func<TPart> built, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
   {
      _scenarioOfTheClass ??= ScenarioOfAClass.Of(GetType());
      return _scenarioOfTheClass.Part($"{file}:{line}", () =>
      {
         _buildingTheScenarioOfTheClass = true;
         try
         {
            return built();
         }
         finally
         {
            _buildingTheScenarioOfTheClass = false;
         }
      });
   }

   /// <summary>Does <paramref name="done"/> once for all the facts of this specification's class, as <see cref="Once{TPart}"/> builds a part.</summary>
   protected void Once(Action done, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
      Once(() =>
      {
         done();
         return true;
      }, file, line);

   /// <summary>
   /// <paramref name="started"/>, ended as the scenario that started it ends: its class's, when started in
   /// <see cref="Once{TPart}"/>, else this fact's, when it is done.
   /// </summary>
   protected TStarted EndWithTheScenario<TStarted>(TStarted started) where TStarted : IDisposable
   {
      StartedNow.Started(started);
      return started;
   }

   /// <summary>Runs <paramref name="onTheUIThread"/> on the UI thread, once the jobs posted to it before have run, and returns what it returns.</summary>
   protected static TResult OnTheUIThread<TResult>(Func<TResult> onTheUIThread) => TheHeadlessUIThread.Run(onTheUIThread);

   /// <summary>Runs <paramref name="onTheUIThread"/> on the UI thread, once the jobs posted to it before have run.</summary>
   protected static void OnTheUIThread(Action onTheUIThread) => TheHeadlessUIThread.Run(onTheUIThread);

   /// <summary>Starts <paramref name="doing"/> on the UI thread and waits until it is done, as a click's handler would run it.</summary>
   protected void DoneOnTheUIThread(Func<Task> doing)
   {
      var done = OnTheUIThread(doing);
      WaitUntil(() => done.IsCompleted, "the UI thread was done");
      done.GetAwaiter().GetResult();
   }

   /// <summary>
   /// Waits until <paramref name="condition"/>, checked on the UI thread after a render tick, which runs layout and
   /// animations: a <see cref="TimeoutException"/> naming <paramref name="what"/> after
   /// <see cref="HowLongWaitingMayTake"/>, once the windows open are drawn as they were then, in
   /// <c>RenderedWindows/&lt;the specification&gt;-waited-in-vain-&lt;n&gt;.png</c>, and with what <paramref name="seen"/> tells
   /// of what was seen meanwhile, where it is given, such as what a chat showed of a turn as it went.
   /// </summary>
   protected void WaitUntil(Func<bool> condition, string what, Func<string>? seen = null)
   {
      var clock = Stopwatch.StartNew();
      while(!OnTheUIThread(() =>
            {
               AvaloniaHeadlessPlatform.ForceRenderTimerTick();
               return condition();
            }))
      {
         if(clock.Elapsed > HowLongWaitingMayTake)
         {
            var windows = OnTheUIThread(() => WindowsShown.Where(it => it.IsVisible).ToList());
            for(var at = 0; at < windows.Count; at++) Render(windows[at], $"{GetType().Name}-waited-in-vain-{at + 1}");
            var wasSeen = seen is null ? "" : $" Seen meanwhile: {OnTheUIThread(seen)}";
            throw new TimeoutException($"Waited {HowLongWaitingMayTake.TotalSeconds:0} s in vain until {what}. The windows open then are drawn in {Path.Combine(AppContext.BaseDirectory, "RenderedWindows")}.{wasSeen}");
         }

         Thread.Sleep(10);
      }
   }

   /// <summary>Shows the window <paramref name="window"/> makes, on the UI thread: it is closed as the scenario that showed it ends.</summary>
   protected TWindow Open<TWindow>(Func<TWindow> window) where TWindow : Window
   {
      var startedNow = StartedNow;
      return OnTheUIThread(() =>
      {
         var opened = window();
         opened.Show();
         startedNow.Showed(opened);
         return opened;
      });
   }

   /// <summary>The one button in <paramref name="within"/> whose tooltip is <paramref name="tip"/>, as Magnus finds it by hovering.</summary>
   protected static Button TheButtonWithTheTip(Control within, string tip) =>
      OnTheUIThread(() => within.GetLogicalDescendants().OfType<Button>().Single(it => ToolTip.GetTip(it) as string == tip));

   /// <summary>
   /// Presses Ctrl+H in <paramref name="window"/>, which switches <paramref name="tab"/> between its chat, which it shows at
   /// first, and its terminal, and waits until the one it shows has the keyboard: the chat's input box, or the terminal.
   /// </summary>
   private protected void SwitchWithCtrlH(Window window, SessionTab tab)
   {
      var toTheChat = !OnTheUIThread(() => tab.ShowsTheChat);
      Press(window, Key.H, PhysicalKey.H, RawInputModifiers.Control);
      WaitUntil(() => TopLevel.GetTopLevel(window)!.FocusManager!.GetFocusedElement() is Control focused
                      && (toTheChat ? focused.FindAncestorOfType<ChatComposer>() is not null : focused == tab.Terminal || tab.Terminal.IsVisualAncestorOf(focused)),
                toTheChat ? "the chat's input box had the keyboard" : "the terminal had the keyboard");
   }

   /// <summary>Waits until the chat's input box of <paramref name="tab"/> in <paramref name="window"/> has the keyboard, as a tab's selection gives it, the tab showing its chat.</summary>
   private protected void WaitUntilTheInputBoxHasTheKeyboard(Window window, SessionTab tab) =>
      WaitUntil(() => TopLevel.GetTopLevel(window)!.FocusManager!.GetFocusedElement() is Control focused && focused.FindAncestorOfType<ChatComposer>() is {} box && box == tab.Chat?.InputBox,
                "the chat's input box had the keyboard");

   /// <summary>The one button in <paramref name="within"/> that says <paramref name="text"/>, as Magnus reads it.</summary>
   protected static Button TheButtonSaying(Control within, string text) =>
      OnTheUIThread(() => within.GetLogicalDescendants().OfType<Button>().Single(it => it.Content as string == text));

   /// <summary>Clicks <paramref name="button"/> on the UI thread: what handles its click runs as for Magnus's.</summary>
   protected static void Click(Button button) => OnTheUIThread(() => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));

   /// <summary>Gives <paramref name="control"/> the keyboard focus, as Magnus does by clicking into it.</summary>
   protected static void Focus(Control control) => OnTheUIThread(() => control.Focus());

   /// <summary>Types <paramref name="text"/> into what has the keyboard focus in <paramref name="window"/>, as Magnus types it.</summary>
   protected static void Type(Window window, string text) => OnTheUIThread(() => window.KeyTextInput(text));

   /// <summary>
   /// Presses and releases <paramref name="key"/>, the key at <paramref name="physicalKey"/>, with
   /// <paramref name="modifiers"/> held, as Magnus does, into what has the keyboard focus in <paramref name="window"/>.
   /// </summary>
   protected static void Press(Window window, Key key, PhysicalKey physicalKey, RawInputModifiers modifiers) => OnTheUIThread(() =>
   {
      window.KeyPress(key, modifiers, physicalKey, keySymbol: null);
      window.KeyRelease(key, modifiers, physicalKey, keySymbol: null);
   });

   /// <summary>Closes <paramref name="window"/> on the UI thread, as Magnus closes a window.</summary>
   protected static void Close(Window window) => OnTheUIThread(window.Close);

   /// <summary>
   /// Draws <paramref name="window"/> to <c>RenderedWindows/&lt;name&gt;.png</c> beside the specifications' assembly, for
   /// an agent to look at what the specification saw.
   /// </summary>
   /// <remarks>
   /// Written beside it first, then moved into its place, under a lock of the machine's: runs at once may draw the same
   /// picture, as each class nested in a specification's class builds the scenario that draws it anew, and the last one
   /// drawn is the one kept. Two moves onto the same file at once fail, access denied.
   /// </remarks>
   protected static void Render(Window window, string name) => OnTheUIThread(() =>
   {
      AvaloniaHeadlessPlatform.ForceRenderTimerTick();
      Dispatcher.UIThread.RunJobs();
      if(window.CaptureRenderedFrame() is not {} frame) return;

      var picture = Path.Combine(Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "RenderedWindows")).FullName, $"{name}.png");
      var beingDrawn = $"{picture}.{Environment.ProcessId}.being-drawn";
      frame.Save(beingDrawn, PngBitmapEncoderOptions.Default);
      using var movingAPictureIn = IMutex.Local("Cloyd.Specifications.MovingAPictureIn", LockTimeout.Seconds(30));
      movingAPictureIn.Locked(() => File.Move(beingDrawn, picture, overwrite: true));
   });

   /// <summary>Ends this fact's scenario, and tells its class's that the fact is done.</summary>
   public virtual void Dispose()
   {
      _startedForThisFact.Dispose();
      _scenarioOfTheClass?.AFactIsDone();
      GC.SuppressFinalize(this);
   }
}
