using Cloyd.Hub.RunningSessions;
using Cloyd.Pasting;

namespace Cloyd.Chat.Writing;

/// <summary>
/// The chat's input box, where Magnus writes a message to the Claude Code that runs in the tab, as the Claude desktop
/// app's composer is, and sends it: through the hub, which types it into Claude Code's own box as he would.
/// </summary>
/// <remarks>
/// Its keys, as Magnus chose them (2026-11-21; <c>dev_docs/cloyd-as-a-dotnet-application.md</c>, "The input box,
/// Magnus's calls"):
/// <list type="bullet">
/// <item>Ctrl+Enter sends; Enter starts a new line, as in every editor.</item>
/// <item>Nothing interrupts Claude by accident. Escape does nothing to it, nor does sending: a message sent while Claude
/// works waits, and Claude Code takes it when it can. Only the Stop button, shown while Claude works, interrupts.</item>
/// <item>Ctrl+V pastes text as pasted, apart from what he types, and an image with no text on the clipboard as an image,
/// shown as a thumbnail over the box.</item>
/// </list>
/// </remarks>
sealed class InputBoxOfTheChat : ChatComposer
{
   /// <summary>The tip of the button that stops Claude.</summary>
   public const string TipOfTheStopButton = "Stop Claude";

   readonly ClaudeCodeChatInput _input;

   public InputBoxOfTheChat(ClaudeCodeChatInput input, PastedImages pastedImages) : base(pastedImages, "Reply to Claude.", TipOfTheStopButton)
   {
      _input = input;
      Edit(input.Draft);
      _input.Changed += Refresh;
      Refresh();
   }

   public RunningClaudeCode? ClaudeCode
   {
      get => _input.ClaudeCode;
      set
      {
         _input.ClaudeCode = value;
         ShowsTheStopButton = value?.Status.IsBusy == true;
      }
   }

   public bool IsGivingMessages => _input.IsGivingMessages;
   public IReadOnlyList<MessageOnItsWay> MessagesOnTheirWay => _input.MessagesOnTheirWay;
   public event Action? MessagesOnTheirWayChanged;
   public event Action? SentASlashCommand;

   void Refresh()
   {
      Tell(_input.Notice);
      MessagesOnTheirWayChanged?.Invoke();
   }

   protected override void Send()
   {
      if(_input.Send() is { IsASlashCommand: true }) SentASlashCommand?.Invoke();
   }

   protected override Task Stop() => _input.Stop();

   public override void Release()
   {
      _input.Changed -= Refresh;
      base.Release();
   }
}
