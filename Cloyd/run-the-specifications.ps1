<#
.SYNOPSIS
Runs Cloyd's specifications in several runs at once, each a dotnet test of its own with a share of the specification
classes, and says what passed, what was skipped and why, what failed, and where the time went. A skipped fact fails
nothing: the rule on when work is done allows one that names the known bug it waits for.

.DESCRIPTION
Avalonia's headless platform has one UI thread a process, whose keyboard focus is one for the process, so a run takes
its specifications one after another. Several runs at once, each a process of its own, take them side by side: each
scenario has a sandbox of its own, its hub included, so the runs share nothing else.

It builds once, lists the specification classes, and deals them out over the runs by the time each took when last
measured: the longest first, each to the run with the least time so far. A class with no time measured is taken to
take the median. The times of the classes whose facts all passed are kept for the next in obj/specification-times.json
beside the specifications' project.

.PARAMETER Runs
How many runs to start at once: 1 runs them all in one.

.PARAMETER Filter
Only the classes whose full names contain it, such as "History".

.PARAMETER ShowTimes
Also prints the time each class took, the slowest first.

.PARAMETER KeepResults
Keeps each run's trx file, in a folder of its own under the system's temp folder, which it names.

.EXAMPLE
pwsh Cloyd/run-the-specifications.ps1
pwsh Cloyd/run-the-specifications.ps1 -Runs 1 -ShowTimes
pwsh Cloyd/run-the-specifications.ps1 -Filter History
#>
param([int] $Runs = 6, [string] $Filter, [switch] $ShowTimes, [switch] $KeepResults)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'Cloyd.Specifications/Cloyd.Specifications.csproj'
$timesFile = Join-Path $PSScriptRoot 'Cloyd.Specifications/obj/specification-times.json'
$clock = [Diagnostics.Stopwatch]::StartNew()

$built = & dotnet build $project -nologo -v q 2>&1
if ($LASTEXITCODE -ne 0) {
   $built
   exit $LASTEXITCODE
}
$buildTook = $clock.Elapsed

$listed = & dotnet test $project --no-build --list-tests -nologo | Where-Object { $_ -match '^    \S' } | ForEach-Object { $_.Trim() }
$classes = @($listed | ForEach-Object { $_.Substring(0, $_.LastIndexOf('.')) } | Sort-Object -Unique)
if ($Filter) { $classes = @($classes | Where-Object { $_.Contains($Filter) }) }
if ($classes.Count -eq 0) { throw "No specification class's name contains '$Filter'." }

$measured = if (Test-Path $timesFile) { Get-Content $timesFile -Raw | ConvertFrom-Json -AsHashtable } else { @{} }
$measuredTimes = @($classes | Where-Object { $measured.ContainsKey($_) } | ForEach-Object { [double]$measured[$_] } | Sort-Object)
$median = if ($measuredTimes.Count -gt 0) { $measuredTimes[[int][Math]::Floor($measuredTimes.Count / 2)] } else { 1.0 }
function TimeOf([string] $class) { if ($measured.ContainsKey($class)) { [double]$measured[$class] } else { $median } }

$dealt = @(1..[Math]::Min($Runs, $classes.Count) | ForEach-Object { [pscustomobject]@{ Number = $_; Classes = [Collections.Generic.List[string]]::new(); Expected = 0.0 } })
foreach ($class in $classes | Sort-Object { TimeOf $_ } -Descending) {
   $run = $dealt | Sort-Object Expected, Number | Select-Object -First 1
   $run.Classes.Add($class)
   $run.Expected += TimeOf $class
}

$results = Join-Path ([IO.Path]::GetTempPath()) "cloyd-specifications-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
New-Item -ItemType Directory -Force $results | Out-Null
function FileOfAFailureAsTheRunEnds($run) { Join-Path $results "run-$($run.Number)-failure-as-it-ended.txt" }
$runsStarted = $clock.Elapsed
$started = foreach ($run in $dealt) {
   $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
   foreach ($argument in @('test', $project, '--no-build', '-nologo',
                           '--filter', (($run.Classes | ForEach-Object { "FullyQualifiedName~$_." }) -join '|'),
                           '--logger', "trx;LogFileName=run-$($run.Number).trx", '--results-directory', $results)) { $start.ArgumentList.Add($argument) }
   # xUnit tells of a failure as the run ends only its type: WhatEndsWithTheRun writes it whole there.
   $start.Environment['CLOYD_SPECIFICATIONS_FILE_OF_A_FAILURE_AS_THE_RUN_ENDS'] = FileOfAFailureAsTheRunEnds $run
   $start.RedirectStandardOutput = $true
   $start.RedirectStandardError = $true
   $process = [Diagnostics.Process]::Start($start)
   [pscustomobject]@{ Run = $run; Process = $process; Printed = $process.StandardOutput.ReadToEndAsync(); Failure = $process.StandardError.ReadToEndAsync() }
}
foreach ($it in $started) { $it.Process.WaitForExit() }
$runsTook = $clock.Elapsed - $runsStarted

$perClass = @{}
$failures = [Collections.Generic.List[string]]::new()
$skipped = [Collections.Generic.List[string]]::new()
$passed = 0
$failed = 0
foreach ($it in $started) {
   $trxFile = Join-Path $results "run-$($it.Run.Number).trx"
   if (-not (Test-Path $trxFile)) {
      $failures.Add("Run $($it.Run.Number) wrote no results, and exited with $($it.Process.ExitCode):`n$($it.Printed.Result)`n$($it.Failure.Result)")
      $failed += 1
      continue
   }

   $failedBefore = $failed
   $trx = [xml](Get-Content $trxFile -Raw)
   $classOf = @{}
   foreach ($test in $trx.TestRun.TestDefinitions.UnitTest) { $classOf[$test.id] = $test.TestMethod.className }
   foreach ($result in $trx.TestRun.Results.UnitTestResult) {
      if ($result.outcome -eq 'NotExecuted') {
         $skipped.Add("$($result.testName): $($result.Output.ErrorInfo.Message)$($result.Output.StdOut)".TrimEnd())
         continue
      }
      $class = $classOf[$result.testId]
      if (-not $perClass.ContainsKey($class)) { $perClass[$class] = [pscustomobject]@{ Class = $class; Seconds = 0.0; Facts = 0; Longest = 0.0; AllPassed = $true } }
      $timed = $perClass[$class]
      $taken = [TimeSpan]::Parse($result.duration).TotalSeconds
      $timed.Seconds += $taken
      $timed.Facts += 1
      $timed.Longest = [Math]::Max($timed.Longest, $taken)
      if ($result.outcome -eq 'Passed') { $passed += 1 }
      else {
         $failed += 1
         $timed.AllPassed = $false
         $failures.Add("$($result.outcome): $($result.testName)`n$($result.Output.ErrorInfo.Message)`n$($result.Output.ErrorInfo.StackTrace)")
      }
   }

   # Such as when what ends with the run fails, after the last fact: a sandbox whose end failed.
   if ($it.Process.ExitCode -ne 0 -and $failed -eq $failedBefore) {
      $asItEnded = FileOfAFailureAsTheRunEnds $it.Run
      $whatFailed = if (Test-Path $asItEnded) { "As the run ended:`n$(Get-Content $asItEnded -Raw)" } else { "$($it.Printed.Result)`n$($it.Failure.Result)" }
      $failures.Add("Run $($it.Run.Number) exited with $($it.Process.ExitCode), though none of its facts failed:`n$whatFailed")
      $failed += 1
   }

   $times = $trx.TestRun.Times
   $it.Run | Add-Member -NotePropertyName Took -NotePropertyValue ([DateTimeOffset]::Parse($times.finish) - [DateTimeOffset]::Parse($times.start)).TotalSeconds
}

foreach ($timed in $perClass.Values | Where-Object AllPassed) { $measured[$timed.Class] = [Math]::Round($timed.Seconds, 2) }
New-Item -ItemType Directory -Force (Split-Path $timesFile) | Out-Null
$measured | ConvertTo-Json | Set-Content $timesFile

foreach ($failure in $failures) { "$failure`n" }
foreach ($skip in $skipped) { "Skipped: $skip" }
"{0} passed, {1} skipped, {2} failed, in {3:0} s: built in {4:0} s, {5} run(s) at once in {6:0} s" -f $passed, $skipped.Count, $failed, $clock.Elapsed.TotalSeconds, $buildTook.TotalSeconds, $dealt.Count, $runsTook.TotalSeconds
foreach ($run in $dealt) {
   "  run {0}: {1,2} classes, expected {2,5:0.0} s, its facts took {3,5:0.0} s, the run {4,5:0.0} s" -f $run.Number, $run.Classes.Count, $run.Expected, ($run.Classes | ForEach-Object { if ($perClass.ContainsKey($_)) { $perClass[$_].Seconds } } | Measure-Object -Sum).Sum, $run.Took
}
if ($ShowTimes) {
   $perClass.Values | Sort-Object -Property Seconds -Descending | ForEach-Object {
      "{0,7:0.0} s  {1,3} facts  {2,5:0.0} s each  longest {3,5:0.0}  {4}" -f $_.Seconds, $_.Facts, ($_.Seconds / $_.Facts), $_.Longest, $_.Class.Split('.')[-1]
   }
}
if ($KeepResults) { "The runs' results: $results" } else { Remove-Item $results -Recurse -Force }
if ($failed -gt 0) { exit 1 }
