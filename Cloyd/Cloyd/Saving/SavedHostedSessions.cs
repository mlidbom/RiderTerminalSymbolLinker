using System.Text.Json;
using ClaudeTooling.SessionHost.Protocol;

namespace Cloyd.Saving;

/// <summary>The latest session and drafts for each host currently or previously shown in Cloyd.
/// The host ID is unique even when the project has several sessions of the same agent CLI.</summary>
sealed class SavedHostedSessions(DirectoryInfo folder)
{
   static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
   readonly Dictionary<SessionHostId, (string Json, SavedTabRestoration Restoration)> _lastWritten = [];
   long _restorationRevision;

   /// <summary>Changes when a save changes a session's identity or restore folder.</summary>
   public long RestorationRevision => Interlocked.Read(ref _restorationRevision);

   FileWrittenWhole FileOf(SessionHostId host) => new(Path.Combine(folder.FullName, $"{host}.json"));

   public SavedSessionTab? Read(SessionHostId host) =>
      FileOf(host).Text() is {} json ? JsonSerializer.Deserialize<SavedSessionTab>(json, Json) : null;

   public void Save(SavedSessionTab session)
   {
      var json = JsonSerializer.Serialize(session, Json);
      var known = _lastWritten.TryGetValue(session.SessionHost, out var previous);
      if(known && previous.Json == json) return;
      FileOf(session.SessionHost).Write(json);
      var restoration = SavedTabRestoration.Of(session);
      _lastWritten[session.SessionHost] = (json, restoration);
      if(!known || previous.Restoration != restoration) Interlocked.Increment(ref _restorationRevision);
   }

   public void ForgetInMemory(SessionHostId host) => _lastWritten.Remove(host);
}
