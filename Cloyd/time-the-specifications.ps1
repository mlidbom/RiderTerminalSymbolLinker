<#
.SYNOPSIS
Runs Cloyd's specifications and says where their time goes: the seconds each specification class's facts took, the
slowest first.

.DESCRIPTION
xUnit makes a new instance of a class for each of its facts, so a class's scenario, in its constructor, runs once per
fact: a class's time is about its scenario's time times its facts. The results are read from the trx logger's file,
written to a folder of the run's own under the system's temp folder.

.PARAMETER Filter
A dotnet test filter, such as "FullyQualifiedName~History": all of them when left out.

.EXAMPLE
pwsh Cloyd/time-the-specifications.ps1
pwsh Cloyd/time-the-specifications.ps1 -Filter "FullyQualifiedName~History"
#>
param([string] $Filter)

$ErrorActionPreference = 'Stop'
$results = Join-Path ([IO.Path]::GetTempPath()) "cloyd-specification-times-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
$arguments = @('test', (Join-Path $PSScriptRoot 'Cloyd.Specifications/Cloyd.Specifications.csproj'), '-nologo', '--logger', 'trx', '--results-directory', $results)
if ($Filter) { $arguments += @('--filter', $Filter) }
& dotnet @arguments | Select-String -Pattern '^\s+Failed |Failed!|Passed!' | ForEach-Object { $_.Line }

$trx = [xml](Get-Content (Get-ChildItem $results -Filter *.trx -Recurse | Select-Object -First 1).FullName)
$classOf = @{}
foreach ($test in $trx.TestRun.TestDefinitions.UnitTest) { $classOf[$test.id] = $test.TestMethod.className }

$perClass = @{}
foreach ($result in $trx.TestRun.Results.UnitTestResult) {
   $class = $classOf[$result.testId]
   $taken = [TimeSpan]::Parse($result.duration).TotalSeconds
   if (-not $perClass.ContainsKey($class)) { $perClass[$class] = [pscustomobject]@{ Class = $class.Split('.')[-1]; Seconds = 0.0; Facts = 0; Longest = 0.0 } }
   $timed = $perClass[$class]
   $timed.Seconds += $taken
   $timed.Facts += 1
   $timed.Longest = [Math]::Max($timed.Longest, $taken)
}

$total = ($perClass.Values | Measure-Object -Property Seconds -Sum).Sum
"{0} classes, {1} facts, {2:0.0} s in facts; the run from {3} to {4}" -f $perClass.Count, ($perClass.Values | Measure-Object -Property Facts -Sum).Sum, $total, $trx.TestRun.Times.start, $trx.TestRun.Times.finish
$perClass.Values | Sort-Object -Property Seconds -Descending | ForEach-Object {
   "{0,7:0.0} s  {1,3} facts  {2,5:0.0} s each  longest {3,5:0.0}  {4}" -f $_.Seconds, $_.Facts, ($_.Seconds / $_.Facts), $_.Longest, $_.Class
}
Remove-Item $results -Recurse -Force
