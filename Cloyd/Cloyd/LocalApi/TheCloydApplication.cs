using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Compze.Threading;
using Compze.Threading.Interprocess;
using Cloyd.LocalApi.Contract;
using Cloyd.Projects;

namespace Cloyd.LocalApi;

/// <summary>
/// The Cloyd application, the one process on the machine that shows every project's window, as a client reaches it:
/// through its local API, found by its <see cref="AnnouncementOfCloydsLocalApi"/> in <see cref="CloydsFolder"/>.
/// <c>Cloyd.exe --project</c> hands it the project so, and starts it first when none runs.
/// </summary>
/// <remarks>
/// Finding the application and starting it are done in a critical section across the processes of the machine, so that
/// IDEs opening several projects at once, each running the command, start one application, not several.
/// </remarks>
sealed class TheCloydApplication(CloydsFolder folder)
{
   static readonly TimeSpan HowLongStartingMayTake = TimeSpan.FromSeconds(30);
   static readonly TimeSpan BetweenTries = TimeSpan.FromMilliseconds(100);
   static readonly HttpClient PeerLaunches = new() { Timeout = TimeSpan.FromSeconds(110) };

   readonly CloydsFolder _folder = folder;
   readonly HttpClient _http = new() { Timeout = HowLongStartingMayTake };

   /// <summary>The application that serves the <see cref="CloydsFolder"/> Cloyd's environment names.</summary>
   public static TheCloydApplication OfCloydsEnvironment() => new(CloydsFolder.OfCloydsEnvironment());

   /// <summary>
   /// Has the application show the window of <paramref name="project"/>: brought forward when it is open, else opened.
   /// Started first when none runs, outside the jobs of whoever ran this, where they let it (<see cref="StartingTheApplication"/>).
   /// The window comes in front of the others when this process may bring a window there (<see cref="TheForeground"/>).
   /// Returns the window once it shows.
   /// </summary>
   public WindowInTheAnswer ShowTheWindowOf(Project project)
   {
      var clock = Stopwatch.StartNew();
      while(true)
      {
         var announcement = TheOneThatRunsOrStarted();
         TheForeground.MayBeTakenBy(announcement.ProcessId);
         using var request = new HttpRequestMessage(HttpMethod.Post, announcement.AddressOf("windows/show"));
         request.Content = JsonContent.Create(new WindowToShowInTheRequest(project.Folder), options: CloydsLocalApiProtocol.Json);
         if(Answer<WindowInTheAnswer>(announcement, request) is {} shown) return shown;
         if(clock.Elapsed > HowLongStartingMayTake) throw new TimeoutException($"No Cloyd application showed the window of {project} within {HowLongStartingMayTake.TotalSeconds:0} s, at the announcement in {_folder}.");
         Thread.Sleep(BetweenTries); // The application announced stopped, as when its last window closed: the next is started.
      }
   }

   /// <summary>The windows the application shows: null when none runs.</summary>
   public IReadOnlyList<WindowInTheAnswer>? Windows() => TheOneThatRuns() is {} running ? WindowsOf(running) : null;

   /// <summary>Starts Cloyd if necessary and obtains the address of the named helper in its project window.</summary>
   public PeerSessionStarted StartPeerSession(SessionToStartForAPeer session)
   {
      var announcement = TheOneThatRunsOrStarted(requireIndependentLifetime: true);
      using var request = new HttpRequestMessage(HttpMethod.Post, announcement.AddressOf("sessions/start"));
      request.Content = JsonContent.Create(session, options: CloydsLocalApiProtocol.Json);
      return Answer<PeerSessionStarted>(announcement, request, PeerLaunches) ?? throw new InvalidOperationException("Cloyd stopped answering. Retry the same launch to find its tab.");
   }

   /// <summary>The process of the application: null when none runs.</summary>
   public int? ProcessId => TheOneThatRuns()?.ProcessId;

   /// <summary>The announcement of the application that runs, once it answers: the one that runs, else one started now.</summary>
   AnnouncementOfCloydsLocalApi TheOneThatRunsOrStarted(bool requireIndependentLifetime = false)
   {
      using var findingOrStarting = IMutex.Local($"Cloyd.FindingOrStartingTheApplication.{NameOf(_folder)}", LockTimeout.Seconds(2 * (long)HowLongStartingMayTake.TotalSeconds));
      return findingOrStarting.Locked(() =>
      {
         if(TheOneThatRuns() is {} running) return running;

         StartingTheApplication.Start(_folder, requireIndependentLifetime);
         var clock = Stopwatch.StartNew();
         while(true)
         {
            if(TheOneThatRuns() is {} started) return started;
            if(clock.Elapsed > HowLongStartingMayTake) throw new TimeoutException($"The Cloyd application started didn't announce its local API in {_folder} within {HowLongStartingMayTake.TotalSeconds:0} s.");
            Thread.Sleep(BetweenTries);
         }
      });

      static string NameOf(CloydsFolder folder) => string.Concat(folder.Folder.FullName.ToLowerInvariant().Select(it => char.IsAsciiLetterOrDigit(it) ? it : '-'));
   }

   /// <summary>The announcement of the application that runs and answers: null when there is none, or its API doesn't listen, as one left by a crash.</summary>
   AnnouncementOfCloydsLocalApi? TheOneThatRuns() =>
      AnnouncementOfCloydsLocalApi.In(_folder) is {} announcement && WindowsOf(announcement) is not null ? announcement : null;

   IReadOnlyList<WindowInTheAnswer>? WindowsOf(AnnouncementOfCloydsLocalApi announcement)
   {
      using var request = new HttpRequestMessage(HttpMethod.Get, announcement.AddressOf("windows"));
      return Answer<WindowsAnswer>(announcement, request)?.Windows;
   }

   /// <summary>
   /// What the application announced answers <paramref name="request"/>, as a <typeparamref name="TAnswer"/>: null when its
   /// API doesn't listen, and an <see cref="HttpRequestException"/> with its body when it answers with a failure.
   /// </summary>
   TAnswer? Answer<TAnswer>(AnnouncementOfCloydsLocalApi announcement, HttpRequestMessage request, HttpClient? client = null) where TAnswer : class
   {
      if(announcement.Protocol != CloydsLocalApiProtocol.Version)
         throw new InvalidOperationException($"The Cloyd application that runs speaks protocol {announcement.Protocol}, and this Cloyd.exe protocol {CloydsLocalApiProtocol.Version}: close its windows, so that this one starts a new one.");

      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", announcement.Token);
      HttpResponseMessage answer;
      try
      {
         answer = (client ?? _http).Send(request);
      }
      catch(HttpRequestException failure) when(failure.HttpRequestError == HttpRequestError.ConnectionError)
      {
         return null; // The application announced has stopped, or crashed and left its announcement.
      }

      using(answer)
      {
         var body = new StreamReader(answer.Content.ReadAsStream()).ReadToEnd();
         if(!answer.IsSuccessStatusCode) throw new HttpRequestException($"{request.Method} {request.RequestUri?.AbsolutePath}: {(int)answer.StatusCode} {body}");
         return JsonSerializer.Deserialize<TAnswer>(body, CloydsLocalApiProtocol.Json)!;
      }
   }
}
