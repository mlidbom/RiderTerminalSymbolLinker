using Cloyd.Projects;
using Cloyd.Sessions;
using Cloyd.Specifications.Chat.Writing;
using Compze.Must;
using Compze.xUnitBDD;

namespace Cloyd.Specifications.Chat.Status;

/// <summary>
/// The line under a chat's input box tells what Claude does, as the hub reports the stand-in for Claude Code that runs in
/// the tab: idle once it has started, and at work while it works.
/// </summary>
public class When_a_chat_tells_what_Claude_does : SpecificationWritingInAChat
{
   readonly ProjectWindow _window;
   readonly SessionTab _tab;

   public When_a_chat_tells_what_Claude_does() => (_window, _tab) = AChatOfANewSessionIn(Sandbox.Project("the-project"));

   string WhatClaudeDoes => OnTheUIThread(() => _tab.Chat.StatusLine.WhatClaudeDoes);

   [XF] public void it_tells_Claude_is_idle() => WaitUntil(() => _tab.Chat.StatusLine.WhatClaudeDoes == "Idle", "the status line told Claude was idle");

   public class and_Claude_works : When_a_chat_tells_what_Claude_does
   {
      public and_Claude_works()
      {
         TypeAndSend(_window, "Be busy for 3 seconds");
         WaitUntil(() => _tab.Chat.InputBox.ShowsTheStopButton, "the Stop button showed, as Claude worked");
      }

      [XF] public void it_tells_Claude_works() => WhatClaudeDoes.Must().Be("Claude works");
   }
}
