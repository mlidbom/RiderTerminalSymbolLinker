using System.Text.RegularExpressions;
using Cloyd.Projects;
using Cloyd.Sessions;
using Cloyd.Specifications.Chat.Writing;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Status;

/// <summary>
/// The stand-in for Claude Code in a chat's tab, idle once it has started: the working line over the input box doesn't
/// show. What the line says as a turn goes is in <see cref="When_the_hub_tells_of_Claudes_turn"/>.
/// </summary>
public class When_Claude_works_in_a_chats_tab : SpecificationWritingInAChat
{
   readonly ProjectWindow _window;
   readonly SessionTab _tab;

   public When_Claude_works_in_a_chats_tab() => (_window, _tab) = AChatOfANewSessionIn(Sandbox.Project("the-project"));

   string? WorkingLineSays => OnTheUIThread(() => _tab.Chat.WorkingLine.Says);

   [XF] public void while_Claude_is_idle_the_working_line_doesnt_show() => WorkingLineSays.Must().BeNull();

   [XF] public void while_Claude_is_idle_the_tabs_header_doesnt_show_that_it_works() => OnTheUIThread(() => _tab.ShowsThatItsSessionWorks).Must().BeFalse();

   /// <summary>Then Magnus has Claude work for 3 seconds.</summary>
   public class and_Claude_works : When_Claude_works_in_a_chats_tab
   {
      public and_Claude_works()
      {
         TypeAndSend(_window, "Be busy for 3 seconds");
         WaitUntil(() => _tab.Chat.InputBox.ShowsTheStopButton, "the Stop button showed, as Claude worked");
         Render(_window, "a-tab-whose-claude-works");
      }

      [XF] public void the_working_line_says_what_Claude_does_and_how_long_the_turn_has_taken() =>
         WorkingLineSays.Must().Satisfy(it => it is not null && Regex.IsMatch(it, @"^[^(]+… \(\d+s"));

      /// <summary>Magnus, 2026-11-21: "get the progress animation icon into the tabs so that I can see which tabs have a working session in them".</summary>
      [XF] public void the_tabs_header_shows_that_it_works_with_the_spinner() => OnTheUIThread(() => _tab.ShowsThatItsSessionWorks).Must().BeTrue();

      [XF] public void once_Claude_is_done_the_working_line_goes() => WaitUntil(() => _tab.Chat.WorkingLine.Says is null, "the working line went");

      [XF] public void once_Claude_is_done_the_spinner_in_the_tabs_header_goes() => WaitUntil(() => !_tab.ShowsThatItsSessionWorks, "the spinner in the tab's header went");
   }
}
