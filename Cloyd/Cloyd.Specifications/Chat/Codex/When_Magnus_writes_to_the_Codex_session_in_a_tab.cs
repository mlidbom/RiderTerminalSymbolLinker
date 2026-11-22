using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using Cloyd.Chat;
using Cloyd.Chat.Views;
using Cloyd.Codex;
using Cloyd.Codex.Chat;
using Cloyd.Codex.Chat.Status;
using Cloyd.Projects;
using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;
using LiveMarkdown.Avalonia;

namespace Cloyd.Specifications.Chat.Codex;

/// <summary>A new, unnamed Codex session in a project window. Its first prompt is sent before it has any
/// saved history. Both views see the same prompt and answer.</summary>
public class When_Magnus_writes_to_the_Codex_session_in_a_tab : SpecificationOpeningWindows
{
   readonly ProjectWindow _window;
   readonly SessionTab _tab;
   readonly CodexSessionId _session;
   ChatOfACodexSession Chat => _tab.CodexChat!;

   public When_Magnus_writes_to_the_Codex_session_in_a_tab() => (_window, _tab, _session) = Once(() =>
   {
      var window = Open(Sandbox.Project("codex-chat").Window);
      DoneOnTheUIThread(() => window.StartANewSession(new NewSession(AgentCli.Codex, Name: null)));
      var tab = OnTheUIThread(() => window.Tabs.Single());
      WaitUntil(() => tab.CodexChat?.InputBox.CanSend == true && tab.SessionRunningInIt?.Session is CodexSession, "the empty Codex chat could accept its first prompt");
      var session = OnTheUIThread(() => (CodexSessionId)tab.SessionRunningInIt!.Session.Id);
      WaitUntilTheInputBoxHasTheKeyboard(window, tab);
      Type(window, "first message from Chat");
      Press(window, Key.Enter, PhysicalKey.Enter, RawInputModifiers.Control);
      WaitUntil(() => tab.CodexChat!.Shown.OfType<CodexReply>().Any(it => it.Markdown == "Heard: first message from Chat") && tab.Terminal.TextShown.Contains("Heard: first message from Chat"),
                "both views showed the reply to the first prompt", () => tab.CodexChat!.InputBox.Notice ?? tab.CodexChat.Notice ?? tab.Terminal.TextShown);
      return (window, tab, session);
   });

   [XF] public void the_terminal_and_Chat_still_show_the_session_in_which_the_tab_started() => OnTheUIThread(() => _tab.LatestSession!.Id).Must().Be(_session);
   [XF] public void the_first_prompt_is_shown_once() => OnTheUIThread(() => Chat.Shown.OfType<ShownInTheChat.Prompt>().Select(it => it.Text).ToArray()).Must().SequenceEqual(["first message from Chat"]);

   public class and_Magnus_opens_Chat_while_an_approval_is_pending : When_Magnus_writes_to_the_Codex_session_in_a_tab
   {
      public and_Magnus_opens_Chat_while_an_approval_is_pending() => Once(() =>
      {
         SwitchWithCtrlH(_window, _tab);
         TypeInTerminal("Ask for approval");
         WaitUntil(() => _tab.SessionRunningInIt is Cloyd.Hub.RunningSessions.RunningCodex { Status.AsReported: "waiting" }, "the terminal was waiting for the approval");
         SwitchWithCtrlH(_window, _tab);
         SeeTheApprovalAndAnswerInTerminal();
      });

      [XF] public void the_terminal_answered_once_and_the_turn_finished() => NumberOfApprovalReplies().Must().Be(1);
   }

   public class and_an_approval_is_requested_while_Chat_is_open : When_Magnus_writes_to_the_Codex_session_in_a_tab
   {
      public and_an_approval_is_requested_while_Chat_is_open() => Once(() =>
      {
         SendFromChat("Ask for approval");
         SeeTheApprovalAndAnswerInTerminal();
      });

      [XF] public void the_terminal_answered_once_and_the_turn_finished() => NumberOfApprovalReplies().Must().Be(1);
   }

   public class and_Magnus_steers_then_stops_a_turn_started_in_Terminal : When_Magnus_writes_to_the_Codex_session_in_a_tab
   {
      public and_Magnus_steers_then_stops_a_turn_started_in_Terminal() => Once(() =>
      {
         SwitchWithCtrlH(_window, _tab);
         TypeInTerminal("Be busy for 60 seconds");
         WaitUntil(() => _tab.ShowsThatItsSessionWorks, "the turn started in Terminal");
         SwitchWithCtrlH(_window, _tab);
         WaitUntil(() => Chat.InputBox.ShowsTheStopButton, "Chat was following the running turn");
         SendFromChat("additional instruction");
         WaitUntil(() => Chat.InputBox.ShowsTheStopButton && Chat.Shown.OfType<ShownInTheChat.Prompt>().Any(it => it.Text == "additional instruction"), "Codex accepted the steer into the running turn");
         Click(TheButtonWithTheTip(Chat, "Stop Codex"));
         WaitUntil(() => !Chat.InputBox.ShowsTheStopButton && Chat.Shown.OfType<EndOfACodexTurn>().Any(it => it.Status == "interrupted"), "the same turn ended as interrupted");
      });

      [XF] public void the_interruption_also_appears_in_Terminal() => OnTheUIThread(() => _tab.Terminal.TextShown).Must().Contain("Conversation interrupted");

      [XF] public void the_interruption_belongs_to_the_steered_turn() => OnTheUIThread(() =>
         Chat.Shown.OfType<ShownInTheChat.Prompt>().Single(it => it.Text == "additional instruction").Id
             .StartsWith(Chat.Shown.OfType<EndOfACodexTurn>().Single(it => it.Status == "interrupted").Id + "/", StringComparison.Ordinal)).Must().BeTrue();
   }

   public class and_Magnus_sends_a_pasted_image_with_no_text : When_Magnus_writes_to_the_Codex_session_in_a_tab
   {
      public and_Magnus_sends_a_pasted_image_with_no_text() => Once(() =>
      {
         PasteAndSendAnImage();
      });

      [XF] public void the_image_is_sent_and_the_composer_is_empty() => OnTheUIThread(() => Chat.InputBox.Draft.IsEmpty && Chat.InputBox.Thumbnails.Count == 0).Must().BeTrue();
   }

   public class and_Magnus_steers_a_running_turn_with_an_image : When_Magnus_writes_to_the_Codex_session_in_a_tab
   {
      readonly bool _steered;
      public and_Magnus_steers_a_running_turn_with_an_image() => _steered = Once(() =>
      {
         SendFromChat("Be busy for 60 seconds");
         WaitUntil(() => Chat.InputBox.ShowsTheStopButton, "the busy turn had started");
         PasteAndSendAnImage();
         var stayedActive = OnTheUIThread(() => Chat.InputBox.ShowsTheStopButton);
         Click(TheButtonWithTheTip(Chat, "Stop Codex"));
         return stayedActive;
      });

      [XF] public void the_image_joined_the_current_turn() => _steered.Must().BeTrue();
   }

   public class and_Chat_reopens_in_the_middle_of_the_reply : When_Magnus_writes_to_the_Codex_session_in_a_tab
   {
      readonly string _idOfThePartialReply;

      public and_Chat_reopens_in_the_middle_of_the_reply() => _idOfThePartialReply = Once(() =>
      {
         SendFromChat("Stream a reply");
         WaitUntil(() => Chat.Shown.OfType<CodexReply>().Any(it => it.Markdown == "Heard: ") && Chat.Progress.WorkingLine?.StartsWith("Writing… (") == true,
                   "the first part of the reply arrived with the writing phase and turn clock", () => Chat.Progress.WorkingLine ?? Chat.Notice ?? "");
         var partial = OnTheUIThread(() => Chat.Shown.OfType<CodexReply>().Single(it => it.Markdown == "Heard: "));
         SwitchWithCtrlH(_window, _tab);
         SwitchWithCtrlH(_window, _tab);
         WaitUntil(() => Chat.Shown.OfType<CodexReply>().Any(it => it.Markdown == "Heard: Stream a reply"), "the reply completed after Chat reopened");
         WaitUntil(() => Chat.Shown.LastOrDefault() is EndOfACodexTurn { Status: "completed", Took: not null, Ended: not null }
                         && Chat.Progress.WorkingLine is null && Chat.StatusLine.TextShown.Contains("codex-chat"),
                   "the completion line replaced live progress and the project's Codex footer stayed visible", () => $"{Chat.StatusLine.TextShown} | {Chat.Shown.LastOrDefault()?.TextShown}");
         return partial.Id;
      });

      [XF] public void the_finished_reply_occupies_the_partial_reply_s_row() =>
         OnTheUIThread(() => Chat.Shown.OfType<CodexReply>().Single(it => it.Id == _idOfThePartialReply).Markdown).Must().Be("Heard: Stream a reply");

      [XF] public void the_turn_finishes_with_its_recorded_duration_and_local_end_time() =>
         OnTheUIThread(() => Chat.Shown.Last().TextShown).Must().Satisfy(it => it.StartsWith("Worked for ") && it.Contains(" · done "));

      [XF] public void the_semibold_timing_and_its_divider_sit_together_beneath_the_reply() => OnTheUIThread(() =>
      {
         var end = Chat.GetVisualDescendants().OfType<TurnEndLine>().Last();
         var reply = Chat.GetVisualDescendants().OfType<Border>().Last(it => it.Child is MarkdownRenderer);
         var label = end.Children.OfType<MarkdownTextBlock>().Single();
         var divider = end.Children.OfType<Border>().Single();
         var colour = ColoursOfTheChat.Text.In(end.ActualThemeVariant);
         label.FontWeight.Must().Be(FontWeight.SemiBold);
         ((ISolidColorBrush)label.Foreground!).Color.Must().Be(colour);
         ((ISolidColorBrush)divider.Background!).Color.Must().Be(colour);
         divider.Bounds.Height.Must().Be(1.5d);
         (divider.Bounds.Y - label.Bounds.Bottom).Must().Be(1d);
         (end.TranslatePoint(default, reply)!.Value.Y - reply.Bounds.Height).Must().Satisfy(gap => Math.Abs(gap - 4) <= 1);
      });
   }

   public class and_Terminal_changes_to_another_session : When_Magnus_writes_to_the_Codex_session_in_a_tab
   {
      public and_Terminal_changes_to_another_session() => Once(() =>
      {
         SwitchWithCtrlH(_window, _tab);
         TypeInTerminal("/new");
         WaitUntil(() => _tab.LatestSession is CodexSession next && next.Id != _session, "the tab followed the new Codex session");
         TypeInTerminal("first message from Terminal");
         WaitUntil(() => _tab.Terminal.TextShown.Contains("Heard: first message from Terminal"), "the new session completed its first turn");
         SwitchWithCtrlH(_window, _tab);
         WaitUntil(() => Chat.Shown.OfType<CodexReply>().Any(it => it.Markdown == "Heard: first message from Terminal"), "Chat showed the new session's history");
      });

      [XF] public void the_old_sessions_prompt_has_gone() => OnTheUIThread(() => Chat.Shown.OfType<ShownInTheChat.Prompt>().Select(it => it.Text).ToArray()).Must().SequenceEqual(["first message from Terminal"]);
   }

   void SeeTheApprovalAndAnswerInTerminal()
   {
      WaitUntil(() => _tab.ShowsTheChat && Chat.WaitingForMagnusInTheTerminal.WhatFor is not null && Chat.InputBox.ShowsTheStopButton, "the chat showed the pending terminal approval");
      Click(TheButtonSaying(Chat, Cloyd.Chat.Status.WaitingForMagnusInTheTerminal.TextOfTheButton));
      WaitUntil(() => !_tab.ShowsTheChat && _tab.Terminal.IsKeyboardFocusWithin, "Magnus chose to answer in Terminal");
      Type(_window, "y");
      WaitUntil(() => _tab.Terminal.TextShown.Contains("Heard: Ask for approval (approved)"), "Codex continued after the terminal approved");
      SwitchWithCtrlH(_window, _tab);
      WaitUntil(() => !Chat.InputBox.ShowsTheStopButton && NumberOfApprovalReplies() == 1, "Chat showed the completed approval turn once");
   }

   int NumberOfApprovalReplies() => OnTheUIThread(() => Chat.Shown.OfType<CodexReply>().Count(it => it.Markdown == "Heard: Ask for approval (approved)"));

   void TypeInTerminal(string text)
   {
      Type(_window, text);
      Press(_window, Key.Enter, PhysicalKey.Enter, RawInputModifiers.None);
   }

   void SendFromChat(string text)
   {
      Type(_window, text);
      Press(_window, Key.Enter, PhysicalKey.Enter, RawInputModifiers.Control);
   }

   void PasteAndSendAnImage()
   {
      using var bitmap = OnTheUIThread(() => new WriteableBitmap(new PixelSize(16, 16), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul));
      DoneOnTheUIThread(() => _window.Clipboard!.SetBitmapAsync(bitmap));
      Press(_window, Key.V, PhysicalKey.V, RawInputModifiers.Control);
      WaitUntil(() => Chat.InputBox.Thumbnails.Count == 1, "the pasted image appeared over the editor");
      Press(_window, Key.Enter, PhysicalKey.Enter, RawInputModifiers.Control);
      WaitUntil(() => Chat.Shown.OfType<ShownInTheChat.Prompt>().Any(it => it.Text == "[Image #1]"), "Codex received the image attachment", () => Chat.InputBox.Notice ?? "");
   }
}
