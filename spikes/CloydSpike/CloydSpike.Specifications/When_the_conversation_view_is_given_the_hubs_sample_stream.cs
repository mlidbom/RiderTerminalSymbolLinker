using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CloydSpike.Chat;
using CloydSpike.Hub;
using Compze.Must;
using Compze.xUnitBDD;
using static CloydSpike.Specifications.HeadlessCloydApp;

namespace CloydSpike.Specifications;

/// <summary>
/// The conversation view given the stream the hub's own contract test samples, <c>conversation-stream.http</c> in
/// <c>~/.claude/src/ClaudeTooling.Specifications/Hub/LocalApi/Samples</c>: every kind of message, a tool's result, a
/// rewind, and the message after it. Drawn to <c>RenderedWindows/Conversation.png</c> in the output folder.
/// </summary>
public class When_the_conversation_view_is_given_the_hubs_sample_stream
{
   static readonly string Sample = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "src", "ClaudeTooling.Specifications", "Hub", "LocalApi", "Samples", "conversation-stream.http");

   readonly List<EventOfAConversation> _events;
   readonly (int MessagesShown, long BytesDrawn) _shown;

   public When_the_conversation_view_is_given_the_hubs_sample_stream()
   {
      _events = EventOfAConversation.ReadFrom(new StringReader(File.ReadAllText(Sample)), CancellationToken.None).ToBlockingEnumerable().ToList();
      _shown = OnTheUiThread(() =>
      {
         var view = new ConversationView();
         var window = new Window { Width = 900, Height = 900, Content = view };
         window.Show();
         foreach(var conversationEvent in _events) view.Show(conversationEvent);
         Dispatcher.UIThread.RunJobs();
         var drawn = Path.Combine(AppContext.BaseDirectory, "RenderedWindows", "Conversation.png");
         Directory.CreateDirectory(Path.GetDirectoryName(drawn)!);
         (window.CaptureRenderedFrame() ?? throw new InvalidOperationException("The window drew no frame.")).Save(drawn, PngBitmapEncoderOptions.Default);
         window.Close();
         return (view.MessageCount, new FileInfo(drawn).Length);
      });
   }

   [XF] public void it_shows_as_many_messages_as_the_last_caught_up_says_the_conversation_has() =>
      _shown.MessagesShown.Must().Be((int)_events.Last(it => it.Name == "caught-up").Data["messages"]!);

   [XF] public void it_draws_them() => _shown.BytesDrawn.Must().BeGreaterThan(10_000);
}
