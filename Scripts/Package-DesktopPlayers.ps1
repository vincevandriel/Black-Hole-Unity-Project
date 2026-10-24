#requires -Version 5.1
<#
.SYNOPSIS
Packages Windows x64 and macOS Unity release players, or explicitly Windows only.
.DESCRIPTION
Does not build, sign or launch either player. The Python packager preserves Unix
executable permissions and symbolic links, validates every ZIP entry, and extracts
and independently inventories its payload. If Windows denies symbolic-link
creation, their ZIP records are verified separately and the extraction is marked
incomplete; no link is flattened to a regular file. Reports stay outside the ZIP.
SourceRevision is the caller-declared Windows revision; MacSourceRevision defaults
to it. These declarations are not proof of binary-to-source identity.
ZIPs have no repeated outer folder. Windows-only players are at the archive root.
Use a short output path; verification paths must fit the packager's 240-unit budget.
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
