using Avalonia.Input.Platform;
using Cloyd.Chat.Writing;
using Cloyd.Sessions;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Writing;

/// <summary>
/// Magnus has typed and pasted into a chat's input box, and the window closes, as Cloyd's restart closes it: the
/// project's next window brings back what the box held.
/// </summary>
public class When_a_project_window_closes_with_a_message_half_written_in_a_chat : SpecificationWritingInAChat
{
   readonly SessionTab _tabBroughtBack;

   public When_a_project_window_closes_with_a_message_half_written_in_a_chat() => _tabBroughtBack = Once(() =>
   {
      var project = Sandbox.Project("the-project");
      var (window, _) = AChatOfANewSessionIn(project);
      Type(window, "Half written, ");
      PasteWithCtrlV(window, clipboard => clipboard.SetTextAsync("and pasted"));
      Close(window);
      var next = Open(project.Window);
      WaitUntil(() => next.BringingBackTheTabs.IsCompleted, "the next window had brought back its tabs");
      return OnTheUIThread(() => next.Tabs.Single());
   });

   [XF] public void its_box_holds_what_was_written_again() => TextIn(_tabBroughtBack).Must().Be("Half written, and pasted");

   [XF] public void each_part_comes_back_as_it_came_in() =>
      OnTheUIThread(() => _tabBroughtBack.Chat.InputBox.Draft.Parts).Must().Satisfy(it => it.SequenceEqual([new PartOfAMessage.TypedText("Half written, "), new PartOfAMessage.PastedText("and pasted")]));
}
