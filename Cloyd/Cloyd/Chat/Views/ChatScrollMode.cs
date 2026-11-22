namespace Cloyd.Chat.Views;

/// <summary>What controls the transcript's position as its contents change.</summary>
enum ChatScrollMode
{
   FollowingTheEnd,
   ReadingInPlace,
   MayResumeAtTheEnd,
   DraggingTheScrollBar
}
