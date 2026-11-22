<#
.SYNOPSIS
Records Cloyd's open project tabs or compares them after a restart.
.DESCRIPTION
Use -Record <file> before a reload, then -Compare <file> afterwards. The record
contains project paths, session host IDs, selected hosts and SHA-256 fingerprints
of saved drafts. A difference can also be caused by editing or sending during the reload.
Neither mode opens windows or restarts Cloyd.
#>
[CmdletBinding(DefaultParameterSetName='Record')]
param(
   [Parameter(Mandatory,ParameterSetName='Record')][string] $Record,
   [Parameter(Mandatory,ParameterSetName='Compare')][string] $Compare
)
$ErrorActionPreference = 'Stop'
$folder = Join-Path $env:LOCALAPPDATA 'Cloyd'

function Read-SavedJson([string] $path) {
   $stream = [IO.File]::Open($path, 'Open', 'Read', ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
   $reader = [IO.StreamReader]::new($stream)
   try { $reader.ReadToEnd() | ConvertFrom-Json -AsHashtable } finally { $reader.Dispose() }
}

function Get-DraftFingerprint($tab) {
   $parts = [ordered]@{ claude = @($tab.draft | Where-Object { $null -ne $_ }); codex = @($tab.codexDrafts | Where-Object { $null -ne $_ }) }
   $json = ConvertTo-Json $parts -Depth 100 -Compress
   [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($json)))
}

function Read-OpenProjects([switch] $RequireNewSaves) {
   $announcement = Read-SavedJson (Join-Path $folder 'local-api.json')
   $process = Get-Process -Id $announcement.processId
   $windows = Invoke-RestMethod -Uri ('http://127.0.0.1:' + $announcement.port + '/windows') -Headers @{ Authorization = 'Bearer ' + $announcement.token } -TimeoutSec 5
   $projects = [ordered]@{}
   foreach($window in $windows.windows) {
      $name = ($window.project.ToLowerInvariant() -creplace '[^a-z0-9]', '-') + '.json'
      $file = Get-Item -LiteralPath (Join-Path $folder 'project-windows' $name) -ErrorAction SilentlyContinue
      if(!$file -or ($RequireNewSaves -and $file.LastWriteTimeUtc -le $process.StartTime.ToUniversalTime())) { return }
      $saved = Read-SavedJson $file.FullName
      $tabs = @($saved.tabs | ForEach-Object {
         [ordered]@{ hostId = $_.sessionHost; draft = Get-DraftFingerprint $_ }
      })
      $projects[$window.project] = [ordered]@{ selected = $saved.selectedTab; tabs = $tabs }
   }
   [ordered]@{ processId = $process.Id; projects = $projects }
}

if($PSCmdlet.ParameterSetName -eq 'Record') {
   $before = Read-OpenProjects
   if(!$before) { throw 'A project window has not saved its tabs yet.' }
   ConvertTo-Json $before -Depth 8 | Set-Content -LiteralPath $Record -Encoding utf8
   Write-Output "Recorded $($before.projects.Count) project windows from Cloyd process $($before.processId)."
   return
}

$before = Read-SavedJson $Compare
$clock = [Diagnostics.Stopwatch]::StartNew()
do {
   $after = Read-OpenProjects -RequireNewSaves
   if($after -and $after.processId -eq $before.processId) { throw 'Cloyd is still running the process in the record.' }
   if($after -and $after.projects.Count -ge $before.projects.Count) { break }
   Start-Sleep -Milliseconds 400
} while($clock.Elapsed.TotalSeconds -lt 30)
if(!$after) { throw 'Cloyd has not written fresh saves for its open windows.' }

$differences = @(
   foreach($project in $before.projects.Keys) {
      $old = $before.projects[$project]
      $new = $after.projects[$project]
      if(!$new) { "Project window missing: $project"; continue }
      if($old.selected -ne $new.selected) { "Selected tab changed in $project" }
      if((@($old.tabs.hostId) -join ',') -ne (@($new.tabs.hostId) -join ',')) { "Tab hosts or order changed in $project" }
      foreach($tab in $old.tabs) {
         $match = $new.tabs | Where-Object hostId -eq $tab.hostId | Select-Object -First 1
         if($match -and $match.draft -ne $tab.draft) { "Saved draft changed in $project for host $($tab.hostId)" }
      }
   }
   foreach($project in $after.projects.Keys) {
      if(!$before.projects.Contains($project)) { "Additional project window: $project" }
   }
)
if($differences.Count) { $differences; exit 1 }
$count = ($after.projects.Values | ForEach-Object { $_.tabs.Count } | Measure-Object -Sum).Sum
"Cloyd process $($after.processId) restored $($after.projects.Count) project windows and $count tabs. Host order, selections and saved draft fingerprints match."
