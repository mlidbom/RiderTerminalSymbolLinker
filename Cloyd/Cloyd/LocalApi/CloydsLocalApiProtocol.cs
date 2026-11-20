using System.Text.Encodings.Web;
using System.Text.Json;

namespace Cloyd.LocalApi;

/// <summary>
/// The protocol of the Cloyd application's local API, the loopback HTTP and JSON through which <c>Cloyd.exe --project</c>
/// hands it a project, and through which the plugin and the hub are to reach it: its version, which a client checks in
/// the <see cref="AnnouncementOfCloydsLocalApi"/>, and its JSON options.
/// </summary>
/// <remarks>
/// As the hub's, the API only adds: a reader ignores what it doesn't know, and the version is raised only by a change that
/// a client written for the earlier one would get wrong.
/// </remarks>
static class CloydsLocalApiProtocol
{
   /// <summary>The version of the protocol: raised only by a change that a client written for the earlier one would get wrong.</summary>
   public const int Version = 1;

   /// <summary>The options the API's JSON is written and read with: with only what JSON requires escaped, so that a Swedish folder's name reads as it is.</summary>
   public static JsonSerializerOptions Json { get; } = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
}
