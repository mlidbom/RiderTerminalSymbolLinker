namespace Cloyd.Chat.ToolCalls;

/// <summary>What a <see cref="ChangeToAFile"/> did to a <see cref="LineOfAChange"/>.</summary>
enum WhatHappenedToTheLine
{
   Unchanged,
   Removed,
   Added
}
