param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$dtetRepo = Split-Path -Parent $PSScriptRoot
$dtetPlayer = Join-Path $dtetRepo 'BinaryBlackHole\Builds\VisualQA\BinaryBlackHole_VisualQA.exe'
if (!(Test-Path -LiteralPath $dtetPlayer)) { throw 'Build the development QA player with DT-ET > Validate Assets and Build QA Player.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$dtetOutput = (Resolve-Path -LiteralPath $OutputDirectory).Path
$dtetLog = Join-Path $dtetOutput 'player.log'
$dtetReceiptPath = Join-Path $dtetOutput 'field-reference-integration.json'
$dtetEnvironmentNames = @('DTET_FIELD_QA_PATH','DTET_NUMERICAL_QA_PATH','DTET_PHYSICS_QA_PATH','DTET_CAPTURE_PATH','DTET_CAPTURE_VARIANT','DTET_VIDEO_PATH','DTET_FFMPEG_PATH')
$dtetPreviousEnvironment = @{}
foreach ($dtetName in $dtetEnvironmentNames) {
    $dtetPreviousEnvironment[$dtetName] = [Environment]::GetEnvironmentVariable($dtetName, 'Process')
}
$dtetStarted = [DateTime]::UtcNow
try {
    foreach ($dtetName in $dtetEnvironmentNames) { [Environment]::SetEnvironmentVariable($dtetName, $null, 'Process') }
    [Environment]::SetEnvironmentVariable('DTET_FIELD_QA_PATH', $dtetOutput, 'Process')
    $dtetProcess = Start-Process -FilePath $dtetPlayer -ArgumentList @('--reference-lab','-screen-width','1600','-screen-height','900','-screen-fullscreen','0','-logFile',('"'+$dtetLog+'"')) -WindowStyle Hidden -PassThru
} finally {
    foreach ($dtetName in $dtetEnvironmentNames) {
        [Environment]::SetEnvironmentVariable($dtetName, $dtetPreviousEnvironment[$dtetName], 'Process')
    }
}
$dtetDeadline = [DateTime]::UtcNow.AddSeconds(150)
while (!$dtetProcess.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $dtetDeadline) {
        # Only this newly launched QA child is stopped; no other Unity/player process is touched.
        if (!$dtetProcess.HasExited) { Stop-Process -Id $dtetProcess.Id -ErrorAction SilentlyContinue }
        throw "Owned field-reference QA process $($dtetProcess.Id) timed out. Inspect $dtetLog."
    }
}
if ($dtetProcess.ExitCode -ne 0 -or !(Test-Path -LiteralPath $dtetReceiptPath)) { throw "Field-reference integration failed: exit $($dtetProcess.ExitCode). Inspect $dtetLog." }
if ((Get-Item -LiteralPath $dtetReceiptPath).LastWriteTimeUtc -lt $dtetStarted) { throw "Field-reference receipt was not written by this run. Inspect $dtetLog." }
$dtetReceipt = Get-Content -LiteralPath $dtetReceiptPath -Raw | ConvertFrom-Json
$dtetFailures = @(Select-String -LiteralPath $dtetLog -Pattern 'Exception:|DTET_FIELD_FAILED|DTET_FIELD_REFERENCE_INTEGRATION_FAILED|^Shader error|error CS\d')
$dtetPasses = @(Select-String -LiteralPath $dtetLog -Pattern '^DTET_FIELD_REFERENCE_INTEGRATION_PASS$')
$dtetFailedChecks = @($dtetReceipt.checks | Where-Object { $_ -notlike 'PASS *' })
if (!$dtetReceipt.success -or $dtetReceipt.checks.Count -eq 0 -or $dtetFailedChecks.Count -gt 0 -or $dtetFailures.Count -gt 0 -or $dtetPasses.Count -ne 1) { throw "Field-reference integration failed. Inspect $dtetLog." }
foreach ($dtetImage in $dtetReceipt.images) {
    $dtetImagePath = Join-Path $dtetOutput $dtetImage.file
    if (!(Test-Path -LiteralPath $dtetImagePath) -or (Get-Item -LiteralPath $dtetImagePath).LastWriteTimeUtc -lt $dtetStarted) { throw "Missing or stale field-reference capture: $dtetImagePath" }
}
Write-Output "PASS field-reference player integration: $($dtetReceipt.checks.Count) checks; $($dtetReceipt.graphicsDevice); $dtetReceiptPath"
