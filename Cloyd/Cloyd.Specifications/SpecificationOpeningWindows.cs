using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;

namespace Cloyd.Specifications;

/// <summary>
/// A specification that opens Cloyd's windows on Avalonia's headless platform: laid out and drawn with Skia, on no
/// screen, on the one UI thread of the headless session, which every specification of the assembly shares. The windows
/// it opens are closed when it is done.
/// </summary>
/// <remarks>
/// Modelled on JAStudio's and Deskmancer's <c>SpecificationOpeningWindows</c>.
/// </remarks>
public abstract class SpecificationOpeningWindows : IDisposable
{
   static readonly TimeSpan HowLongWaitingMayTake = TimeSpan.FromSeconds(30);

   static readonly HeadlessUnitTestSession HeadlessSession = HeadlessUnitTestSession.StartNew(typeof(HeadlessCloydApp), AvaloniaTestIsolationLevel.PerAssembly);

   static readonly Thread TheUIThread = HeadlessSession.Dispatch(() => Thread.CurrentThread, CancellationToken.None).GetAwaiter().GetResult();

   readonly List<Window> _opened = [];

   /// <summary>Runs <paramref name="onTheUIThread"/> on the UI thread, once the jobs posted to it before have run, and returns what it returns.</summary>
   protected static TResult OnTheUIThread<TResult>(Func<TResult> onTheUIThread) =>
      Thread.CurrentThread == TheUIThread
         ? onTheUIThread()
         : HeadlessSession.Dispatch(() =>
         {
            Dispatcher.UIThread.RunJobs();
            return onTheUIThread();
         }, CancellationToken.None).GetAwaiter().GetResult();

   /// <summary>Runs <paramref name="onTheUIThread"/> on the UI thread, once the jobs posted to it before have run.</summary>
   protected static void OnTheUIThread(Action onTheUIThread) => OnTheUIThread(() =>
   {
      onTheUIThread();
      return true;
   });

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
   /// <c>RenderedWindows/&lt;the specification&gt;-waited-in-vain-&lt;n&gt;.png</c>.
   /// </summary>
   protected void WaitUntil(Func<bool> condition, string what)
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
            var windows = OnTheUIThread(() => _opened.Where(it => it.IsVisible).ToList());
            for(var at = 0; at < windows.Count; at++) Render(windows[at], $"{GetType().Name}-waited-in-vain-{at + 1}");
            throw new TimeoutException($"Waited {HowLongWaitingMayTake.TotalSeconds:0} s in vain until {what}. The windows open then are drawn in {Path.Combine(AppContext.BaseDirectory, "RenderedWindows")}.");
         }

         Thread.Sleep(10);
      }
   }

   /// <summary>Shows the window <paramref name="window"/> makes, on the UI thread: it is closed when the specification is done.</summary>
   protected TWindow Open<TWindow>(Func<TWindow> window) where TWindow : Window => OnTheUIThread(() =>
   {
      var opened = window();
      opened.Show();
      _opened.Add(opened);
      return opened;
   });

   /// <summary>The one button in <paramref name="within"/> whose tooltip is <paramref name="tip"/>, as Magnus finds it by hovering.</summary>
   protected static Button TheButtonWithTheTip(Control within, string tip) =>
      OnTheUIThread(() => within.GetLogicalDescendants().OfType<Button>().Single(it => ToolTip.GetTip(it) as string == tip));

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
   protected static void Render(Window window, string name) => OnTheUIThread(() =>
   {
      AvaloniaHeadlessPlatform.ForceRenderTimerTick();
      Dispatcher.UIThread.RunJobs();
      var folder = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "RenderedWindows"));
      window.CaptureRenderedFrame()?.Save(Path.Combine(folder.FullName, $"{name}.png"), PngBitmapEncoderOptions.Default);
   });

   public virtual void Dispose()
   {
      OnTheUIThread(() =>
      {
         foreach(var window in _opened) window.Close();
      });
      GC.SuppressFinalize(this);
   }
}
