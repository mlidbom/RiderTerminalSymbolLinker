using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ClaudeTooling.SessionHost.Protocol;
using CloydSpike;
using CloydSpike.Chat;
using CloydSpike.Hub;
using CloydSpike.SessionHosts;
using CloydSpike.TuiHosts;

// CloydSpike.Probe start | send <text> | tui [<candidate>...] | conversation [<session id>] | end
AppBuilder.Configure<CloydApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
var hub = TheHub.Announced();
var output = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "probe-output"));

switch(args[0])
{
   case "start":
      var started = Off(() => ThrowawaySession.Start(hub));
      Console.WriteLine($"Session host {started.Id} started; Claude Code typed into its shell.");
      for(var tries = 0; tries < 60 && ThrowawaySessionNow() is null; tries++) Thread.Sleep(1000);
      Console.WriteLine(ThrowawaySessionNow()?.ToString() ?? "No Claude Code runs in it yet.");
      break;
   case "send":
      var session = ThrowawaySessionNow() ?? throw new InvalidOperationException("No throwaway session runs.");
      Console.WriteLine(Off(() => hub.Send(session, args[1])) ?? "Sent.");
      Thread.Sleep(3000);
      for(var tries = 0; tries < 120 && ThrowawaySessionNow()!.Status != "idle"; tries++) Thread.Sleep(1000);
      Console.WriteLine(ThrowawaySessionNow());
      break;
   case "tui":
      var named = args.Skip(1).ToList();
      foreach(var candidate in TuiHost.Candidates.Where(it => it.DrawsHeadless && (named.Count == 0 || named.Any(name => it.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase)))))
         ProbeOf(candidate);
      break;
   case "conversation":
      var sessionId = args.Length > 1 ? args[1] : ThrowawaySessionNow()!.SessionId;
      DrawTheConversationOf(sessionId);
      break;
   case "look":
      LookAtTheThrowawayHost(pressingEnter: args.Contains("enter"));
      break;
   case "end":
      foreach(var host in Off(hub.SessionHosts).Where(it => it.IsTheSpikes)) OffAndWait(() => hub.End(host));
      Console.WriteLine("Ended.");
      break;
}

return;

RunningSession? ThrowawaySessionNow()
{
   var hosts = Off(hub.SessionHosts).Where(it => it.IsTheSpikes).Select(it => it.Id).ToHashSet();
   return Off(hub.RunningSessions).FirstOrDefault(it => it.SessionHost is { } host && hosts.Contains(host));
}

void ProbeOf(TuiHost candidate)
{
   var session = ThrowawaySessionNow() ?? throw new InvalidOperationException("No throwaway session runs: start one.");
   var folder = Directory.CreateDirectory(Path.Combine(output.FullName, candidate.Name.Split(' ')[0]));
   foreach(var old in folder.GetFiles()) old.Delete();
   using var typedLog = new StreamWriter(Path.Combine(folder.FullName, "typed.log"));
   var stepNow = "attaching";
   using var attachment = AttachmentToASessionHost.To(new SessionHostId(session.SessionHost!), new ConsoleSize(120, 30));
   attachment.WasTyped += typed => { lock(typedLog) typedLog.WriteLine($"{stepNow}: {Visible(typed)}"); };
   attachment.Ended += why => Console.WriteLine($"  {candidate.Name}: let go, {why}");

   var shown = candidate.Showing(attachment);
   var window = new Window { Width = 1200, Height = 760, Content = shown };
   window.Show();
   Pump(TimeSpan.FromSeconds(4));
   shown.Focus();
   var step = 0;

   Step("attached");
   Type("hello from the probe");
   Step("typed-text");
   Press(PhysicalKey.Escape);
   Pump(TimeSpan.FromMilliseconds(300));
   Press(PhysicalKey.Escape);
   Step("escape-twice");
   Type("/");
   Step("slash-commands-picker");
   Press(PhysicalKey.Escape);
   Step("picker-closed");
   Type("a");
   Press(PhysicalKey.Enter, RawInputModifiers.Shift);
   Type("b");
   Step("shift-enter-between-a-and-b");
   Press(PhysicalKey.Escape);
   Pump(TimeSpan.FromMilliseconds(300));
   Press(PhysicalKey.Escape);
   var copied = window.Clipboard!.SetTextAsync("pasted line one\npasted line two\npasted line three");
   while(!copied.IsCompleted) Pump(TimeSpan.FromMilliseconds(10));
   Press(PhysicalKey.V, RawInputModifiers.Control | RawInputModifiers.Shift);
   Step("ctrl-shift-v");
   Press(PhysicalKey.V, RawInputModifiers.Control);
   Step("ctrl-v");
   Press(PhysicalKey.Escape);
   Pump(TimeSpan.FromMilliseconds(300));
   Press(PhysicalKey.Escape);
   window.Width = 1500;
   window.Height = 900;
   Step("resized-larger");
   for(var notch = 0; notch < 10; notch++) window.MouseWheel(new Point(600, 400), new Vector(0, 1));
   Step("wheel-up-ten-notches");
   for(var notch = 0; notch < 10; notch++) window.MouseWheel(new Point(600, 400), new Vector(0, -1));
   Step("wheel-down-ten-notches");
   window.Width = 900;
   window.Height = 600;
   Step("resized-smaller");
   window.Close();
   Console.WriteLine($"  {candidate.Name}: {step} steps, in {folder.FullName}");
   return;

   void Step(string name)
   {
      stepNow = name;
      Pump(TimeSpan.FromSeconds(1.5));
      var prefix = Path.Combine(folder.FullName, $"{++step:00}-{name}");
      (window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame drawn.")).Save(prefix + ".png", PngBitmapEncoderOptions.Default);
      File.WriteAllText(prefix + ".screen.txt", ScreenOf(session.SessionId) + $"\n[attached at {attachment.Size}]\n");
   }

   void Type(string text)
   {
      foreach(var character in text)
      {
         var key = PhysicalKeyOf(character);
         if(key is { } physical) window.KeyPressQwerty(physical, RawInputModifiers.None);
         window.KeyTextInput(character.ToString());
         if(key is { } released) window.KeyReleaseQwerty(released, RawInputModifiers.None);
         Pump(TimeSpan.FromMilliseconds(15));
      }
   }

   void Press(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
   {
      window.KeyPressQwerty(key, modifiers);
      window.KeyReleaseQwerty(key, modifiers);
      Pump(TimeSpan.FromMilliseconds(50));
   }
}

void LookAtTheThrowawayHost(bool pressingEnter)
{
   var host = Off(hub.SessionHosts).First(it => it.IsTheSpikes);
   using var attachment = AttachmentToASessionHost.To(new SessionHostId(host.Id), new ConsoleSize(120, 30));
   var window = new Window { Width = 1200, Height = 760, Content = new IciclecreekTuiHost().Showing(attachment) };
   window.Show();
   Pump(TimeSpan.FromSeconds(3));
   if(pressingEnter)
   {
      ((Control)window.Content!).Focus();
      window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
      window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
      Pump(TimeSpan.FromSeconds(3));
   }
   var image = Path.Combine(output.FullName, "look.png");
   (window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame drawn.")).Save(image, PngBitmapEncoderOptions.Default);
   Console.WriteLine(image);
   window.Close();
}

void DrawTheConversationOf(string sessionId)
{
   var view = new ConversationView();
   var window = new Window { Width = 900, Height = 4000, Content = view };
   window.Show();
   var cancellation = new CancellationTokenSource();
   var caughtUp = false;
   var stopwatch = Stopwatch.StartNew();
   _ = Task.Run(async () =>
   {
      await foreach(var conversationEvent in hub.ConversationOf(sessionId, cancellation.Token))
      {
         Dispatcher.UIThread.Post(() => view.Show(conversationEvent));
         if(conversationEvent.Name == "caught-up") { caughtUp = true; break; }
      }
   });
   while(!caughtUp && stopwatch.Elapsed < TimeSpan.FromSeconds(60)) Pump(TimeSpan.FromMilliseconds(100));
   Pump(TimeSpan.FromSeconds(2));
   Console.WriteLine($"  {view.MessageCount} messages shown, {stopwatch.ElapsedMilliseconds} ms to catch up and lay out.");
   var image = Path.Combine(output.FullName, $"conversation-{sessionId[..8]}.png");
   (window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame drawn.")).Save(image, PngBitmapEncoderOptions.Default);
   Console.WriteLine($"  {image}");
   cancellation.Cancel();
}

static void Pump(TimeSpan duration)
{
   var stopwatch = Stopwatch.StartNew();
   do
   {
      Dispatcher.UIThread.RunJobs();
      AvaloniaHeadlessPlatform.ForceRenderTimerTick();
      Thread.Sleep(10);
   } while(stopwatch.Elapsed < duration);
}

static T Off<T>(Func<Task<T>> asynchronous) => Task.Run(asynchronous).GetAwaiter().GetResult();
static void OffAndWait(Func<Task> asynchronous) => Task.Run(asynchronous).GetAwaiter().GetResult();

static string ScreenOf(string sessionId)
{
   var typing = Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "bin", "ClaudeTooling.exe"), ["typing", "screen", sessionId]) { RedirectStandardOutput = true, UseShellExecute = false })!;
   var screen = typing.StandardOutput.ReadToEnd();
   typing.WaitForExit();
   return screen;
}

static string Visible(byte[] typed) =>
   string.Concat(System.Text.Encoding.UTF8.GetString(typed).Select(it => it switch
   {
      '\x1b' => "\\e",
      '\r' => "\\r",
      '\n' => "\\n",
      '\t' => "\\t",
      < ' ' => $"^{(char)(it + 64)}",
      _ => it.ToString()
   }));

static PhysicalKey? PhysicalKeyOf(char character) =>
   character switch
   {
      >= 'a' and <= 'z' => PhysicalKey.A + (character - 'a'),
      ' ' => PhysicalKey.Space,
      '/' => PhysicalKey.Slash,
      _ => null
   };
