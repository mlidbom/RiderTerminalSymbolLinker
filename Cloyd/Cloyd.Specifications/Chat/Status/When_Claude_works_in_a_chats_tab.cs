using Cloyd.Chat.Status;
using Cloyd.Projects;
using Cloyd.Sessions;
using Cloyd.Specifications.Chat.Writing;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Status;

/// <summary>
/// The stand-in for Claude Code in a chat's tab, idle once it has started: the working line over the input box doesn't
/// show. The stand-in tells the hub of no turn, as a session that doesn't load the <c>tooling-commands</c> mod.
/// </summary>
public class When_Claude_works_in_a_chats_tab : SpecificationWritingInAChat
{
   readonly ProjectWindow _window;
   readonly SessionTab _tab;

   public When_Claude_works_in_a_chats_tab() => (_window, _tab) = AChatOfANewSessionIn(Sandbox.Project("the-project"));

   string? WorkingLineSays => OnTheUIThread(() => _tab.Chat.WorkingLine.Says);

   [XF] public void while_Claude_is_idle_the_working_line_doesnt_show() => WorkingLineSays.Must().BeNull();

   /// <summary>Then Magnus has Claude work for 3 seconds.</summary>
   public class and_Claude_works : When_Claude_works_in_a_chats_tab
   {
      public and_Claude_works()
      {
         TypeAndSend(_window, "Be busy for 3 seconds");
         WaitUntil(() => _tab.Chat.InputBox.ShowsTheStopButton, "the Stop button showed, as Claude worked");
      }

      [XF] public void the_working_line_says_Claude_works_and_how_long_it_has() =>
         WorkingLineSays.Must().Satisfy(it => it is not null && it.StartsWith(WorkingLineOfTheChat.Working + " (", StringComparison.Ordinal));

      [XF] public void once_Claude_is_done_the_working_line_goes() => WaitUntil(() => _tab.Chat.WorkingLine.Says is null, "the working line went");
   }
}
