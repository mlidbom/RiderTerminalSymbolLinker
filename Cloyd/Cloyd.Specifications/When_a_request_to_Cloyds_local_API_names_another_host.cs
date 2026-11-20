using System.Net;
using System.Net.Http.Headers;
using Compze.Must;
using Compze.xUnitBDD;
using Cloyd.LocalApi;
using Cloyd.LocalApi.Contract;
using Cloyd.Specifications.TheSandbox;

namespace Cloyd.Specifications;

/// <summary>
/// A request to show a project's window reaches the Cloyd application's local API naming another host than the API's own
/// address, as a web page's would through a name of its own that resolves to the loopback address. It carries the token.
/// </summary>
public class When_a_request_to_Cloyds_local_API_names_another_host : SpecificationRunningTheCloydApplication
{
   static readonly HttpClient Http = new();

   readonly SandboxProject _project;
   readonly HttpStatusCode _answered;

   public When_a_request_to_Cloyds_local_API_names_another_host()
   {
      _project = Sandbox.Project("the-project");
      var announcement = AnnouncementOfCloydsLocalApi.In(CloydsFolder)!;
      _answered = Answered(() =>
      {
         using var request = new HttpRequestMessage(HttpMethod.Post, announcement.AddressOf("windows/show"));
         request.Content = JsonContent.Create(new WindowToShowInTheRequest(_project.Project.Folder));
         request.Headers.Host = $"cloyd.example:{announcement.Port}";
         request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", announcement.Token);
         return Http.Send(request).StatusCode;
      }, "the API answered");
   }

   private protected override IReadOnlyList<SandboxProject> Projects => [_project];

   [XF] public void it_is_answered_403() => _answered.Must().Be(HttpStatusCode.Forbidden);

   [XF] public void it_opens_no_window() => OnTheUIThread(() => Windows.Open.Count).Must().Be(0);
}
