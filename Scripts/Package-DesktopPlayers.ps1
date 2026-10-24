#requires -Version 5.1
<#
.SYNOPSIS
Packages Windows x64 and macOS Unity release players, or explicitly Windows only.
.DESCRIPTION
Packages existing players without building, signing or launching them.
Preserves Unix permissions and symlinks; verifies ZIP entries and fresh extraction.
Blocked Windows symlinks are verified separately and leave extraction incomplete.
SourceRevision declares Windows provenance; MacSourceRevision defaults to it.
Neither proves binary-to-source identity. Reports stay outside the ZIP.
No repeated outer folder; Windows-only players are at the archive root.
Use a short output path; verification paths must fit the 240-unit UTF-16 budget.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$WindowsBuildDirectory,
    [string]$MacAppPath,
    [switch]$WindowsOnly,
    [Parameter(Mandatory=$true)][ValidatePattern('^[0-9a-fA-F]{40}$')][string]$SourceRevision,
    [ValidatePattern('^[0-9a-fA-F]{40}$')][string]$MacSourceRevision,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_-]{0,79}$')][string]$PackageName = 'DTET',
    [string]$PythonPath = 'python',
    [ValidateSet('unknown','unsigned','ad-hoc','developer-id-notarized')][string]$MacSigningStatus = 'unknown'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$dtetRepo = Split-Path -Parent $PSScriptRoot
$dtetPackager = Join-Path $dtetRepo 'Packaging/Desktop/package_desktop_players.py'
$dtetArguments = @($dtetPackager, '--repo', $dtetRepo, '--windows-build', $WindowsBuildDirectory,
    '--source-revision', $SourceRevision, '--output', $OutputDirectory, '--package-name', $PackageName)
if ($WindowsOnly) {
    if ($MacAppPath) { throw 'Do not supply MacAppPath with WindowsOnly.' }
    if ($MacSourceRevision) { throw 'Do not supply MacSourceRevision with WindowsOnly.' }
    $dtetArguments += '--windows-only'
} else {
    if ([string]::IsNullOrWhiteSpace($MacAppPath)) { throw 'MacAppPath is required unless WindowsOnly is selected.' }
    $dtetArguments += @('--mac-app', $MacAppPath, '--mac-signing-status', $MacSigningStatus)
    if ($MacSourceRevision) { $dtetArguments += @('--mac-source-revision', $MacSourceRevision) }
}
& $PythonPath @dtetArguments
if ($LASTEXITCODE -ne 0) { throw "Desktop packaging failed with exit code $LASTEXITCODE." }
