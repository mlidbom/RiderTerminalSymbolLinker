using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Threading;
using ClaudeTooling.SessionHost.Protocol;
using CloydSpike.SessionHosts;

namespace CloydSpike.TuiHosts;

/// <summary>
/// xterm.js, VS Code's terminal emulator, in Avalonia's <see cref="NativeWebView"/> (WebView2 on Windows): the page
/// <c>XtermJs/terminal.html</c>, which posts what is typed, and is given what the host draws.
/// </summary>
sealed class XtermJsTuiHost : TuiHost
{
   public override string Name => "xterm.js in NativeWebView";

   public override bool DrawsHeadless => false;

   public override Control Showing(AttachmentToASessionHost attachment)
   {
      var page = new Uri(Path.Combine(AppContext.BaseDirectory, "TuiHosts", "XtermJs", "terminal.html"));
      var webView = new NativeWebView();
      var feeding = false;
      webView.WebMessageReceived += (_, posted) =>
      {
         var message = JsonNode.Parse(posted.Body!)!;
         if(message["typed"] is { } typed) attachment.Type(Encoding.UTF8.GetBytes((string)typed!));
         if(message["typedBinary"] is { } binary) attachment.Type(Encoding.Latin1.GetBytes((string)binary!));
         if(message["columns"] is { } columns) attachment.Resize(new ConsoleSize((short)(int)columns, (short)(int)message["rows"]!));
         if(message["ready"] is null || feeding) return;

         feeding = true;
         new Thread(() => FeedWhatTheHostDraws(attachment, webView)) { IsBackground = true, Name = "Feeding xterm.js" }.Start();
      };
      webView.AdapterCreated += (_, _) => webView.Navigate(page);
      return webView;
   }

   static void FeedWhatTheHostDraws(AttachmentToASessionHost attachment, NativeWebView webView)
   {
      var buffer = new byte[64 * 1024];
      int read;
      while((read = attachment.Drawn.Read(buffer)) > 0)
      {
         var script = $"drawn('{Convert.ToBase64String(buffer, 0, read)}')";
         Dispatcher.UIThread.Post(() => _ = webView.InvokeScript(script));
      }
   }
}
