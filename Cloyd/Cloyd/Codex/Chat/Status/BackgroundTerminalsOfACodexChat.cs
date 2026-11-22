using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ClaudeTooling.Hub.LocalApi.Contract.CodexChat;
using Cloyd.Chat.Views;
using Cloyd.Hub;

namespace Cloyd.Codex.Chat.Status;

/// <summary>Counts the terminals Codex has left running and lists their commands on hover. Reads once
/// on opening and at turn boundaries, then every five seconds while the turn or a terminal is running.</summary>
sealed class BackgroundTerminalsOfACodexChat : TextBlock
{
   readonly Func<CodexSessionId, CancellationToken, Task<CodexChatBackgroundTerminals?>> _listTerminals;
   readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromSeconds(5) };
   CodexSessionId? _session;
   CodexTurnId? _turn;
   CancellationTokenSource? _reading;
   bool _unsupported;

   public BackgroundTerminalsOfACodexChat(Func<CodexSessionId, CancellationToken, Task<CodexChatBackgroundTerminals?>> listTerminals)
   {
      _listTerminals = listTerminals;
      IsVisible = false;
      FontSize = 12;
      TextWrapping = Avalonia.Media.TextWrapping.Wrap;
      Margin = new Thickness(28, 0, 28, 4);
      ColoursOfTheChat.Grey.AsTheForegroundOf(this);
      _refresh.Tick += (_, _) => Read();
   }

   protected override Type StyleKeyOverride => typeof(TextBlock);

   public void Observe(CodexSessionId? session, CodexTurnId? turn)
   {
      if(session == _session && turn == _turn) return;
      _refresh.Stop();
      _reading?.Cancel();
      _reading = null;
      if(session != _session)
      {
         _unsupported = false;
         Display(null, null);
      }
      _session = session;
      _turn = turn;
      Read();
   }

   void Read()
   {
      _refresh.Stop();
      if(_unsupported || _session is not {} session) return;
      var reading = _reading = new CancellationTokenSource();
      _ = Read(session, reading);
   }

   async Task Read(CodexSessionId session, CancellationTokenSource reading)
   {
      try
      {
         var listing = await _listTerminals(session, reading.Token);
         if(reading.IsCancellationRequested) return;
         if(listing is null)
         {
            _unsupported = true;
            Display(null, null);
            return;
         }
         var terminals = listing.Terminals;
         Display(terminals.Count switch { 0 => null, 1 => "1 background terminal", _ => $"{terminals.Count} background terminals" },
                 string.Join("\n", terminals.Select(it => it.Command)));
         _refresh.IsEnabled = _turn is not null || terminals.Count > 0;
      }
      catch(OperationCanceledException) when(reading.IsCancellationRequested) {}
      catch(RefusedByTheHub) when(!reading.IsCancellationRequested) { Display(null, null); }
      catch(Exception failure)
      {
         if(reading.IsCancellationRequested) return;
         Display("Background terminals unavailable", failure.Message);
         _refresh.Start();
      }
      finally
      {
         if(_reading == reading) _reading = null;
         reading.Dispose();
      }
   }

   void Display(string? summary, string? commands)
   {
      Text = summary;
      IsVisible = summary is not null;
      ToolTip.SetTip(this, string.IsNullOrEmpty(commands) ? null : commands);
   }
}
