<#
.SYNOPSIS
    Build the plugin using the repo-local JDK 21, without touching the global JAVA_HOME.

.DESCRIPTION
    The Gradle toolchain needs JDK 21. A machine that ships only JBR 25 (e.g. a Rider install) can't
    satisfy that, so the repo carries a pinned Temurin JDK 21 under .tooling\jdk (gitignored, large,
    machine-specific). This wrapper points JAVA_HOME at it FOR THIS PROCESS ONLY -- PowerShell's $env:
    assignments never reach the user or system environment, so nothing global is changed -- and forwards
    every argument to gradlew.bat. With no arguments it runs `buildPlugin`.

.EXAMPLE
    .\build.ps1
    Builds the plugin (buildPlugin); the zip lands in build\distributions\.

.EXAMPLE
    .\build.ps1 clean buildPlugin --console=plain
    Forwards the given tasks and flags straight through to Gradle.
#>

$root = $PSScriptRoot

# Locate the repo-local JDK by glob, so a patch-version refresh of the JDK doesn't break this script.
$jdkDir = Join-Path $root '.tooling\jdk'
$jdk = Get-ChildItem -LiteralPath $jdkDir -Directory -Filter 'jdk-*' -ErrorAction SilentlyContinue |
    Where-Object { Test-Path (Join-Path $_.FullName 'bin\java.exe') } |
    Sort-Object Name -Descending |
    Select-Object -First 1

if (-not $jdk) {
    Write-Error @"
No JDK 21 found under $jdkDir (expected a 'jdk-*' folder containing bin\java.exe).
This machine has no global JDK 21, so the build needs the repo-local one. Restore it by extracting
.tooling\jdk21.zip into .tooling\jdk (or drop any JDK 21 there as .tooling\jdk\jdk-<version>), then
re-run this script.
"@
    exit 1
}

# Scope JAVA_HOME to THIS process only -- it is gone the moment this script exits; nothing global changes.
$env:JAVA_HOME = $jdk.FullName
Write-Host "Using repo-local JDK: $($jdk.FullName)" -ForegroundColor Cyan

# @(...) forces an array: without it PowerShell unwraps a single task to a bare string, and splatting
# a string with @tasks forwards it one character at a time instead of as one argument.
$tasks = @( if ($args.Count -gt 0) { $args } else { 'buildPlugin' } )
& (Join-Path $root 'gradlew.bat') @tasks
exit $LASTEXITCODE
