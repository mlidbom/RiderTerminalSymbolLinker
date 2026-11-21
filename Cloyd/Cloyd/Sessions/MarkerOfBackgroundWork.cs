using Avalonia.Controls;
using Avalonia.Layout;
using Cloyd.Chat.Status;
using Cloyd.Chat.Views;
using Cloyd.Hub;

namespace Cloyd.Sessions;

/// <summary>
/// The marker in a tab's header, before its title, that tells that the tab's Claude Code runs background work while Claude
/// doesn't work, such as a shell command Claude ran in the background, which wakes Claude once done: so a session that
/// waits on something isn't taken for one that is done. Magnus, 2026-11-21: "I have no way in the new UI to see that a
/// session in waiting on something". Claude Code's glyph, ✻, still and grey, where the <see cref="SpinnerOfClaudeCode"/>
/// turns while Claude works, as high and wide; its tip says what runs.
/// </summary>
sealed class MarkerOfBackgroundWork : TextBlock
{
   public MarkerOfBackgroundWork()
   {
      Resources.MergedDictionaries.Add(ColoursOfTheChat.AsResources());
      ColoursOfTheChat.Grey.AsTheForegroundOf(this);
      FontSize = SpinnerOfClaudeCode.SizeOfItsGlyphs;
      Width = SpinnerOfClaudeCode.HowWideItIs;
      Height = SpinnerOfClaudeCode.HowHighItIs;
      LineHeight = SpinnerOfClaudeCode.HowHighItIs;
      VerticalAlignment = VerticalAlignment.Center;
      Text = "✻";
      IsVisible = false;
   }

   protected override Type StyleKeyOverride => typeof(TextBlock);

   /// <summary>What its tip says: what runs in the background, counted, then each task with what it does. Null while it doesn't show.</summary>
   public string? Tip => IsVisible ? ToolTip.GetTip(this) as string : null;

   /// <summary>Shows it while <paramref name="claudeCode"/>, the Claude Code that runs in the tab, runs background work and Claude doesn't work, with what runs in its tip.</summary>
   public void Show(RunningSession? claudeCode)
   {
      var counted = claudeCode is { Status.IsBusy: false } ? claudeCode.BackgroundWorkCounted : null;
      IsVisible = counted is not null;
      ToolTip.SetTip(this, claudeCode?.BackgroundWork is { IsAny: true } work && counted is not null ? $"{counted}\n{work.Listed}" : counted);
   }
}
