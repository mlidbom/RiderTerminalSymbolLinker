using System.Net;
using Compze.Must;
using Compze.xUnitBDD;
using Cloyd.LocalApi;
using Cloyd.LocalApi.Contract;
using Cloyd.Specifications.TheSandbox;

namespace Cloyd.Specifications;

/// <summary>
/// A request to show a project's window reaches the Cloyd application's local API without the token of its announcement,
/// as from any process on the machine that didn't read it.
/// </summary>
public class When_a_request_to_Cloyds_local_API_lacks_its_token : SpecificationRunningTheCloydApplication
{
   static readonly HttpClient Http = new();

   readonly SandboxProject _project = SandboxProject.Named(nameof(When_a_request_to_Cloyds_local_API_lacks_its_token));
   readonly HttpStatusCode _answered;

   public When_a_request_to_Cloyds_local_API_lacks_its_token() : base(nameof(When_a_request_to_Cloyds_local_API_lacks_its_token))
   {
      var announcement = AnnouncementOfCloydsLocalApi.In(CloydsFolder)!;
      _answered = Answered(() => Http.PostAsJsonAsync(announcement.AddressOf("windows/show"), new WindowToShowInTheRequest(_project.Project.Folder)).GetAwaiter().GetResult().StatusCode,
                           "the API answered");
   }

   private protected override IReadOnlyList<SandboxProject> Projects => [_project];

   [XF] public void it_is_answered_401() => _answered.Must().Be(HttpStatusCode.Unauthorized);

   [XF] public void it_opens_no_window() => OnTheUIThread(() => Windows.Open.Count).Must().Be(0);
}
