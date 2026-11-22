using System.Text.Json;
using Cloyd.LocalApi.Contract;
using ClaudeTooling.SessionHost.Protocol;

namespace Cloyd.LocalApi;

/// <summary>The two irreversible steps of a coordinator's launch are recorded before their requests leave
/// Cloyd. After a restart it can find the original host, start the CLI if no launch line was attempted, or
/// observe an already submitted line. A completed receipt keeps the original address.</summary>
sealed class PeerSessionLaunchReceipt
{
   readonly FileWrittenWhole _file;
   Record? _record;
   public SessionToStartForAPeer Request { get; }

   public PeerSessionLaunchReceipt(CloydsFolder folder, SessionToStartForAPeer request)
   {
      Request = request;
      _file = new FileWrittenWhole(Path.Combine(folder.Folder.FullName, "peer-session-starts", $"{request.RequestId:D}.json"));
      _record = _file.Text() is {} text ? JsonSerializer.Deserialize<Record>(text)! : null;
      if(_record is not null) RequireSameRequest(_record.Request);
   }

   public void RequireSameRequest(SessionToStartForAPeer original)
   {
      if(!original.Matches(Request)) throw new PeerSessionRequestConflict();
   }

   public bool HostWasRequested => _record is not null;
   public string? HostWhereCliWasRequested => _record?.HostWhereCliWasRequested;
   public PeerSessionStarted? Session => _record?.Session;

   public void RecordHostRequest() => Write(new Record(Request, null, null));
   public void RecordCliRequest(SessionHostId host) => Write(new Record(Request, host.Value, null));

   public PeerSessionStarted RecordSession(Sessions.Session session)
   {
      var answer = new PeerSessionStarted($"{Request.AgentCli}:{session.Id.Value}");
      Write(new Record(Request, HostWhereCliWasRequested, answer));
      return answer;
   }

   void Write(Record record)
   {
      _file.Write(JsonSerializer.Serialize(record));
      _record = record;
   }

   sealed record Record(SessionToStartForAPeer Request, string? HostWhereCliWasRequested, PeerSessionStarted? Session);
}
