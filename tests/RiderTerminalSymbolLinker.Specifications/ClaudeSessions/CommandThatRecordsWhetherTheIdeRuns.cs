using System.Diagnostics;

namespace RiderTerminalSymbolLinker.Specifications.ClaudeSessions;

/// <summary>
///   A command for the IDE's restarter to run before it starts the IDE again, as a deploy's puts a build in: it waits
///   three seconds, then records whether the IDE's process <paramref name="ide"/> still runs. What it found, and when it
///   ended, tell whether the restarter ran it once the IDE had exited, and waited for it before starting the IDE.
/// </summary>
public sealed class CommandThatRecordsWhetherTheIdeRuns
{
   readonly string _record;

   public CommandThatRecordsWhetherTheIdeRuns(int ide)
   {
      var folder = Directory.CreateTempSubdirectory("before-it-starts-again-").FullName;
      _record = Path.Combine(folder, "record.txt");
      var script = Path.Combine(folder, "record.ps1");
      File.WriteAllText(script, $"Start-Sleep -Seconds 3; Set-Content -Path '{_record}' -Value $(if (Get-Process -Id {ide} -ErrorAction SilentlyContinue) {{ 'runs' }} else {{ 'gone' }})");
      Argv = ["powershell.exe", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script];
   }

   /// <summary>The command: its program and arguments.</summary>
   public IReadOnlyList<string> Argv { get; }

   /// <summary>What it found of the IDE's process, <c>runs</c> or <c>gone</c>, or null when it hasn't run.</summary>
   public string? WhatItFound => File.Exists(_record) ? File.ReadAllText(_record).Trim() : null;

   /// <summary>When it ended, or <see cref="DateTime.MaxValue"/> when it hasn't run.</summary>
   public DateTime Ended => File.Exists(_record) ? File.GetLastWriteTimeUtc(_record) : DateTime.MaxValue;

   /// <summary>When the process <paramref name="ide"/> started.</summary>
   public static DateTime StartOf(int ide)
   {
      using var process = Process.GetProcessById(ide);
      return process.StartTime.ToUniversalTime();
   }
}
