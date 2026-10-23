param(
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [ValidateRange(2,15)][double]$BenchmarkSeconds = 3,
    [ValidateRange(120,1200)][int]$TimeoutSeconds = 480
)
$ErrorActionPreference = 'Stop'
$dtetRepo = Split-Path -Parent $PSScriptRoot
$dtetPlayer = Join-Path $dtetRepo 'BinaryBlackHole\Builds\VisualQA\BinaryBlackHole_VisualQA.exe'
if (!(Test-Path -LiteralPath $dtetPlayer)) { throw 'Build the development QA player with DT-ET > Validate Assets and Build QA Player.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$dtetOutput = (Resolve-Path -LiteralPath $OutputDirectory).Path
$dtetLog = Join-Path $dtetOutput 'player.log'
$dtetReceiptPath = Join-Path $dtetOutput 'reduced-exhibit-integration.json'
$dtetEnvironmentNames = @('DTET_REDUCED_QA_PATH','DTET_REDUCED_QA_BENCHMARK_SECONDS','DTET_FIELD_QA_PATH','DTET_NUMERICAL_QA_PATH','DTET_PHYSICS_QA_PATH','DTET_CAPTURE_PATH','DTET_CAPTURE_VARIANT','DTET_VIDEO_PATH','DTET_FFMPEG_PATH')
$dtetPreviousEnvironment = @{}
foreach ($dtetName in $dtetEnvironmentNames) { $dtetPreviousEnvironment[$dtetName] = [Environment]::GetEnvironmentVariable($dtetName, 'Process') }
$dtetStarted = [DateTime]::UtcNow
try {
    foreach ($dtetName in $dtetEnvironmentNames) { [Environment]::SetEnvironmentVariable($dtetName, $null, 'Process') }
    [Environment]::SetEnvironmentVariable('DTET_REDUCED_QA_PATH', $dtetOutput, 'Process')
    [Environment]::SetEnvironmentVariable('DTET_REDUCED_QA_BENCHMARK_SECONDS', $BenchmarkSeconds.ToString([Globalization.CultureInfo]::InvariantCulture), 'Process')
    $dtetProcess = Start-Process -FilePath $dtetPlayer -ArgumentList @('--reduced-exhibit','-screen-width','1600','-screen-height','900','-screen-fullscreen','0','-logFile',('"'+$dtetLog+'"')) -WindowStyle Hidden -PassThru
} finally {
    foreach ($dtetName in $dtetEnvironmentNames) { [Environment]::SetEnvironmentVariable($dtetName, $dtetPreviousEnvironment[$dtetName], 'Process') }
}
$dtetMemory = [Collections.Generic.List[object]]::new()
$dtetPeakWorking = 0L
$dtetPeakPrivate = 0L
$dtetDeadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
$dtetTimedOut = $false
while (!$dtetProcess.WaitForExit(250)) {
    try {
        $dtetProcess.Refresh()
        $dtetWorking = $dtetProcess.WorkingSet64
        $dtetPrivate = $dtetProcess.PrivateMemorySize64
        $dtetPeakWorking = [Math]::Max($dtetPeakWorking, $dtetWorking)
        $dtetPeakPrivate = [Math]::Max($dtetPeakPrivate, $dtetPrivate)
        $dtetMemory.Add([ordered]@{elapsedSeconds=([DateTime]::UtcNow-$dtetStarted).TotalSeconds; workingSetBytes=$dtetWorking; privateBytes=$dtetPrivate})
    } catch { if (!$dtetProcess.HasExited) { throw } }
    if ([DateTime]::UtcNow -gt $dtetDeadline) {
        # Stop only the QA child created by this invocation, never another user's Unity session.
        if (!$dtetProcess.HasExited) { Stop-Process -Id $dtetProcess.Id -ErrorAction SilentlyContinue }
        $dtetTimedOut = $true
        break
    }
}
[ordered]@{
    startedUtc=$dtetStarted.ToString('o'); finishedUtc=[DateTime]::UtcNow.ToString('o');
    processId=$dtetProcess.Id; player=$dtetPlayer; timedOut=$dtetTimedOut;
    samplingIntervalMilliseconds=250; peakSampledWorkingSetBytes=$dtetPeakWorking; peakSampledPrivateBytes=$dtetPeakPrivate;
    scope='Windows process memory sampled every 250 ms across launcher, all profiles, fallback, seek/capture/performance and unload. Includes validation-only full-image captures and managed/native/driver-associated process allocations, but is not a dedicated GPU VRAM measurement; shorter transient peaks can be missed. Hidden development player with explicit completed off-screen rendering and a one-pixel GPU completion readback per timed frame; not native presentation FPS, laptop or headset certification.';
    samples=$dtetMemory.ToArray()
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $dtetOutput 'process-memory.json') -Encoding utf8
if ($dtetTimedOut) { throw "Owned reduced-exhibit QA process $($dtetProcess.Id) timed out. Inspect $dtetLog." }
if ($dtetProcess.ExitCode -ne 0 -or !(Test-Path -LiteralPath $dtetReceiptPath)) { throw "Reduced-exhibit integration failed: exit $($dtetProcess.ExitCode). Inspect $dtetLog." }
if ((Get-Item -LiteralPath $dtetReceiptPath).LastWriteTimeUtc -lt $dtetStarted) { throw "Reduced-exhibit receipt was not written by this run. Inspect $dtetLog." }
$dtetReceipt = Get-Content -LiteralPath $dtetReceiptPath -Raw | ConvertFrom-Json
$dtetFailures = @(Select-String -LiteralPath $dtetLog -Pattern 'Exception:|DTET_REDUCED_FAILED|DTET_REDUCED_EXHIBIT_INTEGRATION_FAILED|^Shader error|error CS\d')
$dtetPasses = @(Select-String -LiteralPath $dtetLog -Pattern '^DTET_REDUCED_EXHIBIT_INTEGRATION_PASS$')
$dtetFailedChecks = @($dtetReceipt.checks | Where-Object { $_ -notlike 'PASS *' })
if (!$dtetReceipt.success -or $dtetReceipt.checks.Count -eq 0 -or $dtetFailedChecks.Count -gt 0 -or $dtetFailures.Count -gt 0 -or $dtetPasses.Count -ne 1) { throw "Reduced-exhibit integration failed. Inspect $dtetLog." }
foreach ($dtetImage in $dtetReceipt.images) {
    $dtetImagePath = Join-Path $dtetOutput $dtetImage.file
    if (!(Test-Path -LiteralPath $dtetImagePath) -or (Get-Item -LiteralPath $dtetImagePath).LastWriteTimeUtc -lt $dtetStarted) { throw "Missing or stale reduced-exhibit capture: $dtetImagePath" }
}
if ($dtetMemory.Count -lt 4 -or $dtetPeakWorking -le 0 -or $dtetPeakPrivate -le 0) { throw 'Process memory sampling did not collect a usable trace.' }
Write-Output "PASS reduced-exhibit player integration: $($dtetReceipt.checks.Count) checks; $($dtetReceipt.graphicsDevice); $dtetReceiptPath"
Write-Output "Measured process peaks: working set $dtetPeakWorking bytes, private bytes $dtetPeakPrivate; $($dtetReceipt.performance.Count) completed off-screen frame windows."
