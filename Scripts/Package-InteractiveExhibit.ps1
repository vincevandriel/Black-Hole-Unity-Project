#requires -Version 5.1
<#
.SYNOPSIS
Packages an already-built, already-tested Windows x64 development player.
.DESCRIPTION
Packages without building or launching Unity. TestedRevision is caller-declared;
passing receipts and build markers do not establish binary-to-source identity.
Retain separate build/revision evidence. Sorted ZIP entries use the tested commit
timestamp; identical inputs and compressor produce identical contents.
.EXAMPLE
.\Scripts\Package-InteractiveExhibit.ps1 -BuildDirectory C:\build\VisualQA `
  -TestedRevision <full-40-character-commit> -ValidationReceipt C:\qa\interactive-exhibit-integration.json `
  -BuildLog C:\qa\build.log -OutputDirectory C:\delivery -VerifyExtractedPackage
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$BuildDirectory,
    [Parameter(Mandatory=$true)][ValidatePattern('^[0-9a-fA-F]{40}$')][string]$TestedRevision,
    [Parameter(Mandatory=$true)][string]$ValidationReceipt,
    [string]$GeneratedFluidReceipt,
    [Parameter(Mandatory=$true)][string]$BuildLog,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_-]{0,79}$')][string]$PackageName = 'DTET_Interactive_Observatory_Development_Candidate',
    [switch]$VerifyExtractedPackage
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$dtetRepo = Split-Path -Parent $PSScriptRoot
$dtetUtf8 = [Text.UTF8Encoding]::new($false)

function Write-Utf8([string]$Path, [string]$Value) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path)) | Out-Null
    [IO.File]::WriteAllText($Path, $Value, $dtetUtf8)
}
function Get-Sha([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Get-Relative([string]$Base, [string]$Path) {
    $prefix = $Base.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (!$Path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw "Path escaped its input root: $Path" }
    $Path.Substring($prefix.Length).Replace('\', '/')
}
function Get-Inventory([string]$Base) {
    $paths = [Collections.Generic.List[string]]::new()
    foreach ($file in Get-ChildItem -LiteralPath $Base -File -Recurse -Force) {
        $paths.Add((Get-Relative $Base $file.FullName))
    }
    $paths.Sort([StringComparer]::Ordinal)
    foreach ($relative in $paths) {
        $path = Join-Path $Base $relative
        [ordered]@{path=$relative; bytes=(Get-Item -LiteralPath $path).Length; sha256=(Get-Sha $path)}
    }
}
function Assert-Inventory([string]$Base, [object[]]$Expected) {
    $actual = @(Get-Inventory $Base)
    if ($actual.Count -ne $Expected.Count) { throw "File count mismatch in $Base" }
    for ($i = 0; $i -lt $Expected.Count; $i++) {
        if ($actual[$i].path -cne $Expected[$i].path -or $actual[$i].bytes -ne $Expected[$i].bytes -or $actual[$i].sha256 -cne $Expected[$i].sha256) {
            throw "Hash or path mismatch in $Base : $($Expected[$i].path)"
        }
    }
}

$dtetBuild = (Resolve-Path -LiteralPath $BuildDirectory).Path.TrimEnd('\', '/')
if (!(Test-Path -LiteralPath $dtetBuild -PathType Container)) { throw 'BuildDirectory must be a directory.' }
$dtetReceiptPath = (Resolve-Path -LiteralPath $ValidationReceipt).Path
$dtetLogPath = (Resolve-Path -LiteralPath $BuildLog).Path
$dtetReceipt = Get-Content -LiteralPath $dtetReceiptPath -Raw | ConvertFrom-Json
$dtetChecks = @($dtetReceipt.checks)
if ($dtetReceipt.success -isnot [bool] -or !$dtetReceipt.success -or $dtetChecks.Count -eq 0 -or @($dtetChecks | Where-Object { $_ -isnot [string] -or $_ -cnotlike 'PASS *' }).Count -gt 0) {
    throw 'ValidationReceipt must contain success:true and a nonempty list of passing checks.'
}
if ($dtetReceipt.performanceOnly -isnot [bool] -or $dtetReceipt.performanceOnly) { throw 'Supply the full interactive integration receipt, not a performance-only receipt.' }
$dtetFluidReceiptPath = $null
$dtetFluidPassCount = 0
if ($GeneratedFluidReceipt) {
    $dtetFluidReceiptPath = (Resolve-Path -LiteralPath $GeneratedFluidReceipt).Path
    $dtetFluidReceipt = Get-Content -LiteralPath $dtetFluidReceiptPath -Raw | ConvertFrom-Json
    if ($dtetFluidReceipt.success -isnot [bool] -or !$dtetFluidReceipt.success -or $dtetFluidReceipt.checks.Count -eq 0 -or @($dtetFluidReceipt.checks | Where-Object { $_ -isnot [string] -or $_ -cnotlike 'PASS *' }).Count -gt 0) {
        throw 'GeneratedFluidReceipt must contain success:true and a nonempty list of passing checks.'
    }
    $dtetFluidPassCount = $dtetFluidReceipt.checks.Count
}
$dtetBuildText = Get-Content -LiteralPath $dtetLogPath -Raw
if ($dtetBuildText -notmatch '(?m)^DTET_QA_BUILD_SUCCEEDED(?:[ \t]+[^\r\n]+)?\r?$') { throw 'BuildLog lacks DTET_QA_BUILD_SUCCEEDED from the development QA build.' }
if ($dtetBuildText -match '(?m)^DTET_.*BUILD_FAILED|^error CS\d|Build completed with a result of .Failed.') { throw 'BuildLog contains a build failure marker.' }
$dtetResolvedRevision = (& git -C $dtetRepo rev-parse --verify ($TestedRevision + '^{commit}')).Trim()
if ($LASTEXITCODE -ne 0 -or $dtetResolvedRevision -cne $TestedRevision.ToLowerInvariant()) { throw 'TestedRevision must resolve to the supplied full commit in this repository.' }
$dtetDocumentationRevision = (& git -C $dtetRepo rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not read the packaging checkout revision.' }
$dtetCommitEpochText = (& git -C $dtetRepo show -s --format=%ct $dtetResolvedRevision).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not read tested commit time.' }
$dtetZipTime = [DateTimeOffset]::FromUnixTimeSeconds([long]$dtetCommitEpochText).ToUniversalTime()
if ($dtetZipTime.Year -lt 1980 -or $dtetZipTime.Year -gt 2107) { throw 'Tested commit time cannot be represented in a ZIP timestamp.' }
$dtetZipTime = [DateTimeOffset]::new($dtetZipTime.Year, $dtetZipTime.Month, $dtetZipTime.Day, $dtetZipTime.Hour, $dtetZipTime.Minute, ($dtetZipTime.Second - ($dtetZipTime.Second % 2)), [TimeSpan]::Zero)
$dtetSourceUrl = 'https://github.com/vincevandriel/Black-Hole-Unity-Project'
$dtetOutput = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\', '/')
if ($dtetOutput.Equals($dtetBuild, [StringComparison]::OrdinalIgnoreCase) -or $dtetOutput.StartsWith($dtetBuild + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'OutputDirectory must be outside BuildDirectory.' }
$dtetPackage = Join-Path $dtetOutput $PackageName
$dtetZip = Join-Path $dtetOutput ($PackageName + '.zip')
$dtetManifestPath = Join-Path $dtetOutput ($PackageName + '.package-manifest.json')
$dtetSummaryPath = Join-Path $dtetOutput ($PackageName + '.package-summary.json')
$dtetExtracted = Join-Path $dtetOutput ($PackageName + '_extracted_verification')
foreach ($path in @($dtetPackage, $dtetZip, $dtetManifestPath, $dtetSummaryPath)) {
    if (Test-Path -LiteralPath $path) { throw "Refusing to overwrite: $path" }
}
if ($VerifyExtractedPackage -and (Test-Path -LiteralPath $dtetExtracted)) { throw "Refusing to overwrite: $dtetExtracted" }

# Copy only allowlisted player roots.
$dtetRuntimeRoots = @('BinaryBlackHole_VisualQA.exe','UnityPlayer.dll','UnityCrashHandler64.exe','BinaryBlackHole_VisualQA_Data','MonoBleedingEdge','D3D12','WinPixEventRuntime.dll')
foreach ($name in @('BinaryBlackHole_VisualQA.exe','UnityPlayer.dll','BinaryBlackHole_VisualQA_Data','MonoBleedingEdge')) {
    if (!(Test-Path -LiteralPath (Join-Path $dtetBuild $name))) { throw "Required player root is missing: $name" }
}
$dtetRuntimePaths = [Collections.Generic.List[string]]::new()
foreach ($name in $dtetRuntimeRoots) {
    $path = Join-Path $dtetBuild $name
    if (!(Test-Path -LiteralPath $path)) { continue }
    $items = @((Get-Item -LiteralPath $path -Force))
    if ($items[0].PSIsContainer) { $items += @(Get-ChildItem -LiteralPath $path -Recurse -Force) }
    foreach ($item in $items) {
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse points are not permitted in player input: $($item.FullName)" }
        if ($item.PSIsContainer) { continue }
        $relative = Get-Relative $dtetBuild $item.FullName
        if ($relative -match '(?i)(DoNotShip|DontShip|BurstDebugInformation|(^|/)(backups?|logs?|cache|temp)(/|$)|\.(pdb|mdb|bak|orig|log|tmp)$|~$)') { continue }
        $dtetRuntimePaths.Add($relative)
    }
}
$dtetRuntimePaths.Sort([StringComparer]::Ordinal)
if ($dtetRuntimePaths.Count -lt 10) { throw 'Player runtime inventory is unexpectedly small.' }
$dtetDocuments = @(
    'Docs/INTERACTIVE_OBSERVATORY.md','Docs/INTERACTIVE_OBSERVATORY_VALIDATION.md','Docs/FINAL_DESKTOP_SCOPE.md','Docs/INTERACTIVE_SCENARIO_MODEL.md',
    'Docs/INTERACTIVE_OPTICS.md','Docs/M4_REDUCED_FLOW_MODEL.md','Docs/M4_QUALITY_AND_SKY.md',
    'Docs/GENERATED_FLUID_MODEL.md','Docs/GENERATED_FLUID_VALIDATION.md','Docs/PUBLIC_SIMULATION_ROUTE.md',
    'Docs/PHYSICS_M1.md','Docs/PHYSICS_M2.md','Docs/PHYSICS_M3.md','Docs/PHYSICS_M4.md',
    'Docs/M3_OPTICAL_REFERENCE.md','Docs/Data/SXS0305/README.md','Docs/Data/SXS0305/LICENSE.txt',
    'Docs/Data/SXS0305/provenance.json','Docs/Data/SXS0305/fixtures.json'
)
foreach ($relative in $dtetDocuments) {
    if (!(Test-Path -LiteralPath (Join-Path $dtetRepo $relative) -PathType Leaf)) { throw "Required portable document missing: $relative" }
}
$dtetTemplates = Join-Path $dtetRepo 'Packaging/Interactive'
$dtetTemplateNames = @('READ_ME_FIRST.txt','NOTICES.txt','START_OBSERVATORY.cmd','START_M3_REFERENCE_LAB.cmd','START_M4_REDUCED_EXHIBIT.cmd','START_EVENT_LESSONS.cmd','RUN_DEVICE_CHECK.cmd','RUN_PERFORMANCE_CHECK.cmd','Invoke-DeviceCheck.ps1')
foreach ($name in $dtetTemplateNames) {
    if (!(Test-Path -LiteralPath (Join-Path $dtetTemplates $name) -PathType Leaf)) { throw "Missing package template: $name" }
}
# Adapt only the tested harness's two player-location lines.
$dtetRunner = Get-Content -LiteralPath (Join-Path $dtetRepo 'Scripts/Test-InteractiveExhibit.ps1') -Raw
$dtetOldRepo = '$dtetRepo = Split-Path -Parent $PSScriptRoot'
$dtetOldPlayer = '$dtetPlayer = Join-Path $dtetRepo ''BinaryBlackHole\Builds\VisualQA\BinaryBlackHole_VisualQA.exe'''
foreach ($line in @($dtetOldRepo, $dtetOldPlayer)) {
    if ([regex]::Matches($dtetRunner, [regex]::Escape($line)).Count -ne 1) { throw 'Device runner changed: review its package-path adaptation before continuing.' }
}
$dtetRunner = $dtetRunner.Replace($dtetOldRepo, '$dtetRepo = $PSScriptRoot').Replace($dtetOldPlayer, '$dtetPlayer = Join-Path $dtetRepo ''BinaryBlackHole_VisualQA.exe''')

[IO.Directory]::CreateDirectory($dtetPackage) | Out-Null
$dtetRuntimeManifest = [Collections.Generic.List[object]]::new()
foreach ($relative in $dtetRuntimePaths) {
    $source = Join-Path $dtetBuild $relative
    $destination = Join-Path $dtetPackage $relative
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
    [IO.File]::Copy($source, $destination, $false)
    $sha = Get-Sha $source
    if ((Get-Sha $destination) -cne $sha) { throw "Runtime copy mismatch: $relative" }
    $dtetRuntimeManifest.Add([ordered]@{path=$relative; bytes=(Get-Item -LiteralPath $destination).Length; sha256=$sha})
}
foreach ($name in $dtetTemplateNames) {
    $value = Get-Content -LiteralPath (Join-Path $dtetTemplates $name) -Raw
    $value = $value.Replace('@@TESTED_REVISION@@', $dtetResolvedRevision).Replace('@@SOURCE_URL@@', $dtetSourceUrl).Replace('@@PASS_COUNT@@', [string]$dtetChecks.Count)
    if ($value -match '@@[A-Z_]+@@') { throw "Unresolved template token in $name" }
    if ($name.EndsWith('.cmd')) { $value = $value.Replace("`r`n", "`n").Replace("`n", "`r`n") }
    Write-Utf8 (Join-Path $dtetPackage $name) $value
}
Write-Utf8 (Join-Path $dtetPackage 'Run-DeviceCheck.ps1') $dtetRunner
foreach ($relative in $dtetDocuments) {
    $source = Join-Path $dtetRepo $relative
    $destination = Join-Path $dtetPackage $relative
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
    [IO.File]::Copy($source, $destination, $false)
}
# Keep shipped document links local; point other relative links to the checkout revision.
foreach ($relative in $dtetDocuments | Where-Object { $_.EndsWith('.md') }) {
    $destination = Join-Path $dtetPackage $relative
    $text = [IO.File]::ReadAllText($destination)
    $documentDirectory = [IO.Path]::GetDirectoryName((Join-Path $dtetRepo $relative))
    $rewritten = [regex]::Replace($text, '\]\(([^)\s]+)\)', [Text.RegularExpressions.MatchEvaluator]{ param($match)
        $target = $match.Groups[1].Value
        if ($target -match '^([a-zA-Z][a-zA-Z0-9+.-]*:|#|/)') { return $match.Value }
        $parts = $target.Split('#', 2)
        $resolved = [IO.Path]::GetFullPath((Join-Path $documentDirectory $parts[0]))
        $repoRelative = Get-Relative $dtetRepo $resolved
        if ($dtetDocuments -contains $repoRelative) { return $match.Value }
        $anchor = if ($parts.Length -gt 1) { '#' + $parts[1] } else { '' }
        return '](' + $dtetSourceUrl + '/blob/' + $dtetDocumentationRevision + '/' + $repoRelative + $anchor + ')'
    })
    Write-Utf8 $destination $rewritten
}
[IO.Directory]::CreateDirectory((Join-Path $dtetPackage 'Evidence')) | Out-Null
[IO.File]::Copy($dtetReceiptPath, (Join-Path $dtetPackage 'Evidence/interactive-exhibit-integration.json'), $false)
if ($dtetFluidReceiptPath) { [IO.File]::Copy($dtetFluidReceiptPath, (Join-Path $dtetPackage 'Evidence/generated-fluid-integration.json'), $false) }
$dtetProvenance = 'The caller declares this source revision produced the tested runtime. Build marker and passing receipt are checked; their association with these binary bytes depends on the external build/revision record. Runtime SHA-256 hashes identify the exact packaged bytes.'
$dtetRuntimeRecord = [ordered]@{
    schema='dtet.interactive-runtime-manifest.v1'; packageKind='Windows x64 verification/development candidate';
    callerDeclaredTestedRuntimeRevision=$dtetResolvedRevision; packagingCheckoutRevision=$dtetDocumentationRevision;
    provenance=$dtetProvenance; sourceUrl=$dtetSourceUrl; buildMarker='DTET_QA_BUILD_SUCCEEDED';
    buildLogSha256=(Get-Sha $dtetLogPath); validationReceiptSha256=(Get-Sha $dtetReceiptPath);
    validationPassCount=$dtetChecks.Count; generatedFluidPassCount=$dtetFluidPassCount; copiedRuntimeFiles=$dtetRuntimeManifest.ToArray();
    pending=@('Visible native-window interaction and presentation measurements','Designated laptop measurements','Physical XR hardware and human usability validation','Relativistic fluid/metric evolution and observer ray transfer; public-data generation route documented')
}
Write-Utf8 (Join-Path $dtetPackage 'runtime-manifest.json') ($dtetRuntimeRecord | ConvertTo-Json -Depth 8)
$dtetPackageInventory = @(Get-Inventory $dtetPackage)
Write-Utf8 $dtetManifestPath ([ordered]@{schema='dtet.package-manifest.v1'; packageName=$PackageName; callerDeclaredTestedRuntimeRevision=$dtetResolvedRevision; files=$dtetPackageInventory} | ConvertTo-Json -Depth 8)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$dtetZipStream = [IO.File]::Open($dtetZip, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try {
    $archive = [IO.Compression.ZipArchive]::new($dtetZipStream, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        foreach ($file in $dtetPackageInventory) {
            $entry = $archive.CreateEntry($PackageName + '/' + $file.path, [IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = $dtetZipTime
            $inputStream = [IO.File]::OpenRead((Join-Path $dtetPackage $file.path))
            $outputStream = $entry.Open()
            try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose(); $inputStream.Dispose() }
        }
    } finally { $archive.Dispose() }
} finally { $dtetZipStream.Dispose() }
$archive = [IO.Compression.ZipFile]::OpenRead($dtetZip)
try {
    if ($archive.Entries.Count -ne $dtetPackageInventory.Count) { throw 'ZIP entry count mismatch.' }
    foreach ($file in $dtetPackageInventory) {
        $entry = $archive.GetEntry($PackageName + '/' + $file.path)
        if ($null -eq $entry -or $entry.Length -ne $file.bytes) { throw "ZIP entry missing or wrong size: $($file.path)" }
        $stream = $entry.Open(); $algorithm = [Security.Cryptography.SHA256]::Create()
        try { $sha = [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
        finally { $algorithm.Dispose(); $stream.Dispose() }
        if ($sha -cne $file.sha256) { throw "ZIP hash mismatch: $($file.path)" }
    }
} finally { $archive.Dispose() }
if ($VerifyExtractedPackage) {
    [IO.Compression.ZipFile]::ExtractToDirectory($dtetZip, $dtetExtracted)
    Assert-Inventory (Join-Path $dtetExtracted $PackageName) $dtetPackageInventory
}
Assert-Inventory $dtetPackage $dtetPackageInventory
$dtetSummary = [ordered]@{
    schema='dtet.package-summary.v1'; packageName=$PackageName; callerDeclaredTestedRuntimeRevision=$dtetResolvedRevision;
    developmentCandidate=$true; archiveFile=([IO.Path]::GetFileName($dtetZip)); archiveBytes=(Get-Item -LiteralPath $dtetZip).Length;
    archiveSha256=(Get-Sha $dtetZip); packageManifestSha256=(Get-Sha $dtetManifestPath);
    runtimeFiles=$dtetRuntimeManifest.Count; archiveFiles=$dtetPackageInventory.Count; validationPassCount=$dtetChecks.Count;
    generatedFluidPassCount=$dtetFluidPassCount;
    runtimeCopiesHashVerified=$true; zipEntriesHashVerified=$true; extractedPackageHashVerified=[bool]$VerifyExtractedPackage;
    nativeWindowOrLaptopCertified=$false; physicalXrCertified=$false; provenance=$dtetProvenance
}
Write-Utf8 $dtetSummaryPath ($dtetSummary | ConvertTo-Json -Depth 5)
$dtetSummary | ConvertTo-Json -Depth 5
Write-Output "Package: $dtetZip"
Write-Output "Manifest: $dtetManifestPath"
Write-Output "Summary: $dtetSummaryPath"
