using System.Diagnostics;

namespace Cloyd.Hub;

partial class TheHub
{
   /// <summary>
   /// The time a request gives the hub to answer, from when it is first sent: <see cref="HowLongANewHubMayTakeToAnswer"/>,
   /// time enough for a publish to start the next hub.
   /// </summary>
   sealed class TimeToAnswer(DirectoryInfo folderOfTheAnnouncement)
   {
      readonly DirectoryInfo _folderOfTheAnnouncement = folderOfTheAnnouncement;
      readonly Stopwatch _clock = Stopwatch.StartNew();

      /// <summary>Waits <see cref="BetweenTries"/> before the next try: an <see cref="HttpRequestException"/> instead once the time is up.</summary>
      public async Task WaitBeforeTryingAgain(CancellationToken cancellation)
      {
         if(_clock.Elapsed > HowLongANewHubMayTakeToAnswer)
            throw new HttpRequestException($"No hub answered within {HowLongANewHubMayTakeToAnswer.TotalSeconds:0} s at the announcement in {_folderOfTheAnnouncement.FullName}.");
         await Task.Delay(BetweenTries, cancellation);
      }
   }
}
