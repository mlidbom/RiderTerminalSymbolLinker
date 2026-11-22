using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Cloyd.Chat.Views;
using Cloyd.Codex;
using Cloyd.Codex.Chat;
using Cloyd.Specifications.TheSandbox;
using Compze.Must;
using Compze.xUnitBDD;
using LiveMarkdown.Avalonia;

namespace Cloyd.Specifications.Chat;

public class When_Magnus_clicks_a_link_in_a_reply : SpecificationOpeningWindows
{
   [XF] public void a_single_click_on_a_local_screenshot_link_reaches_the_file_launcher()
   {
      const string label = "Open the screenshot at full size";
      var project = Sandbox.Project("opening-a-screenshot");
      var file = new FileInfo(Path.Combine(project.Project.Folder, "worked-for-divider.png"));
      File.WriteAllBytes(file.FullName, []);
      var destination = file.FullName.Replace('\\', '/');
      var items = new[] { new { id = "reply", type = "agentMessage", text = $"[{label}]({destination})" } };
      var session = new CodexSessionId(Guid.NewGuid().ToString());
      var hub = Sandbox.HubOfTheSandbox;
      new CodexSessionOfASpecification(session, project.Project.Folder, "screenshot") { RecordedItems = JsonSerializer.SerializeToNode(items)!.AsArray() }.WriteItsRecordFor(hub);
      var window = Open(() => new Window { Width = 720, Height = 320, Content = new ChatOfACodexSession(hub.Hub, Sandbox.CloydsFolder.PastedImages, new CodexSessionDrafts()) });
      var chat = OnTheUIThread(() => (ChatOfACodexSession)window.Content!);
      OnTheUIThread(() => chat.Show(session, null));
      WaitUntil(() => chat.GetVisualDescendants().OfType<MarkdownTextBlock>().Any(it => it.LayoutText == label), "the reply link appeared");
      OnTheUIThread(() =>
      {
         var block = chat.GetVisualDescendants().OfType<MarkdownTextBlock>().Single(it => it.LayoutText == label);
         var link = block.Inlines!.OfType<Link>().Single();
         var point = block.GetInlineGeometry(link)!.GetRects(window).First().Center;
         window.MouseMove(point);
         window.MouseDown(point, MouseButton.Left);
         window.MouseUp(point, MouseButton.Left);
         // The headless platform has no system file viewer. Its refusal must be surfaced by the actual chat click.
         chat.Notice.Must().Be($"Couldn't open {link.HRef!.OriginalString}.");
      });
   }

   [XF] public async Task native_paths_and_encoded_file_links_open_the_same_local_image()
   {
      var file = new FileInfo(Path.Combine(Sandbox.Project("opening-image-paths").Project.Folder, "turn end #1.png"));
      File.WriteAllBytes(file.FullName, []);
      var drivePath = file.FullName.Replace('\\', '/');
      var launcher = new RecordingLauncher();
      foreach(var href in new[] { drivePath, drivePath.Replace(" ", "%20").Replace("#", "%23"), new Uri(file.FullName).AbsoluteUri })
      {
         (await ChatLinks.Open(new Uri(href), launcher)!).Must().BeTrue();
         launcher.LastFile!.Must().Be(file.FullName);
      }
   }

   [XF] public async Task a_web_link_opens_as_a_URI_and_other_schemes_do_not_launch()
   {
      var launcher = new RecordingLauncher();
      var url = new Uri("https://example.org/preview");
      (await ChatLinks.Open(url, launcher)!).Must().BeTrue();
      launcher.LastUri.Must().Be(url);
      ChatLinks.Open(new Uri("ms-settings:display"), launcher).Must().BeNull();
      ChatLinks.Open(new Uri("file:///C:/example.exe"), launcher).Must().BeNull();
      launcher.LastFile.Must().BeNull();
   }

   sealed class RecordingLauncher : ILauncher
   {
      public string? LastFile { get; private set; }
      public Uri? LastUri { get; private set; }
      public Task<bool> LaunchFileAsync(IStorageItem item)
      {
         LastFile = item.TryGetLocalPath();
         return Task.FromResult(true);
      }
      public Task<bool> LaunchUriAsync(Uri uri)
      {
         LastUri = uri;
         return Task.FromResult(true);
      }
   }
}
