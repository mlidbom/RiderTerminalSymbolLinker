using ClaudeTooling.SessionHost.Protocol;
using Cloyd.VirtualDesktops;
using Cloyd.Sessions;
using System.Text.Json;

namespace Cloyd.Saving;

/// <summary>The host selected in Projects Dashboard and the Windows virtual desktop where Magnus left it.</summary>
sealed record SavedProjectsDashboard(SessionHostId? Selected, VirtualDesktop? Desktop, Session? OutsideHost = null, SavedDashboardArrangement? Arrangement = null)
{
   static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
   public static SavedProjectsDashboard Read(FileWrittenWhole file) =>
      file.Text() is {} json ? JsonSerializer.Deserialize<SavedProjectsDashboard>(json, Json)! : new(null, null);

   public void Write(FileWrittenWhole file) => file.Write(JsonSerializer.Serialize(this, Json));
}
