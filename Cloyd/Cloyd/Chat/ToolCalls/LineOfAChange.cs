namespace Cloyd.Chat.ToolCalls;

/// <summary>A line of a <see cref="ChangeToAFile"/>: taken out, put in, or left as it was between the lines that were.</summary>
sealed record LineOfAChange(WhatHappenedToTheLine Kind, string Text);
