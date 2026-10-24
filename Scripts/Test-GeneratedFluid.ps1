param(
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$PlayerPath,
    [switch]$DefaultLaunch,
    [ValidateRange(120,1200)][int]$TimeoutSeconds = 480
)
$ErrorActionPreference = 'Stop'
$dtetRepo = Split-Path -Parent $PSScriptRoot
if (!$PlayerPath) { $PlayerPath = Join-Path $dtetRepo 'BinaryBlackHole\Builds\VisualQA\BinaryBlackHole_VisualQA.exe' }
if (!(Test-Path -LiteralPath $PlayerPath -PathType Leaf)) { throw 'Build the Unity QA player first.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$dtetOutput = (Resolve-Path -LiteralPath $OutputDirectory).Path
$dtetLog = Join-Path $dtetOutput 'player.log'
$dtetReceipt = Join-Path $dtetOutput 'generated-fluid-integration.json'
$dtetNames = @('DTET_GENERATED_FLUID_QA_PATH','DTET_INTERACTIVE_QA_PATH','DTET_REDUCED_QA_PATH','DTET_FIELD_QA_PATH','DTET_NUMERICAL_QA_PATH','DTET_PHYSICS_QA_PATH','DTET_CAPTURE_PATH','DTET_VIDEO_PATH')
$dtetPrevious = @{}
foreach ($dtetName in $dtetNames) { $dtetPrevious[$dtetName] = [Environment]::GetEnvironmentVariable($dtetName, 'Process') }
$dtetStarted = [DateTime]::UtcNow
try {
    foreach ($dtetName in $dtetNames) { [Environment]::SetEnvironmentVariable($dtetName, $null, 'Process') }
    [Environment]::SetEnvironmentVariable('DTET_GENERATED_FLUID_QA_PATH', $dtetOutput, 'Process')
    $dtetArguments = @('-screen-width','1600','-screen-height','900','-screen-fullscreen','0','-logFile',('"'+$dtetLog+'"'))
    if (!$DefaultLaunch) { $dtetArguments = @('--interactive-exhibit') + $dtetArguments }
    $dtetProcess = Start-Process -FilePath $PlayerPath -ArgumentList $dtetArguments -WindowStyle Hidden -PassThru
} finally {
    foreach ($dtetName in $dtetNames) { [Environment]::SetEnvironmentVariable($dtetName, $dtetPrevious[$dtetName], 'Process') }
}
$dtetPeakWorking = 0L
$dtetPeakPrivate = 0L
$dtetDeadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
while (!$dtetProcess.WaitForExit(250)) {
    try {
        $dtetProcess.Refresh()
        $dtetPeakWorking = [Math]::Max($dtetPeakWorking, $dtetProcess.WorkingSet64)
        $dtetPeakPrivate = [Math]::Max($dtetPeakPrivate, $dtetProcess.PrivateMemorySize64)
    } catch { if (!$dtetProcess.HasExited) { throw } }
    if ([DateTime]::UtcNow -gt $dtetDeadline) {
        Stop-Process -Id $dtetProcess.Id -ErrorAction SilentlyContinue
        throw "Owned fluid QA process timed out. Inspect $dtetLog."
    }
}
if ($dtetProcess.ExitCode -ne 0 -or !(Test-Path -LiteralPath $dtetReceipt)) { throw "Fluid integration failed: exit $($dtetProcess.ExitCode). Inspect $dtetLog." }
if ((Get-Item -LiteralPath $dtetReceipt).LastWriteTimeUtc -lt $dtetStarted) { throw 'Fluid receipt is stale.' }
$dtetResult = Get-Content -LiteralPath $dtetReceipt -Raw | ConvertFrom-Json
$dtetFailures = @(Select-String -LiteralPath $dtetLog -Pattern 'Exception:|DTET_GENERATED_FLUID_FAILED|^Shader error|error CS\d')
if (!$dtetResult.success -or $dtetResult.checks.Count -eq 0 -or @($dtetResult.checks | Where-Object { $_ -notlike 'PASS *' }).Count -gt 0 -or $dtetFailures.Count -gt 0) { throw "Fluid checks failed. Inspect $dtetLog." }
foreach ($dtetImage in $dtetResult.images) {
    $dtetImagePath = Join-Path $dtetOutput $dtetImage
    if (!(Test-Path -LiteralPath $dtetImagePath) -or (Get-Item -LiteralPath $dtetImagePath).LastWriteTimeUtc -lt $dtetStarted) { throw "Missing/stale capture: $dtetImage" }
}
[ordered]@{
    player=$PlayerPath; startedUtc=$dtetStarted.ToString('o'); finishedUtc=[DateTime]::UtcNow.ToString('o');
    peakSampledWorkingSetBytes=$dtetPeakWorking; peakSampledPrivateBytes=$dtetPeakPrivate;
    scope='Hidden development player, generated fluid plus captures/readbacks; process memory at 250 ms, not dedicated GPU VRAM or native presentation FPS.'
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $dtetOutput 'process-memory.json') -Encoding utf8
Write-Output "PASS generated fluid: $($dtetResult.checks.Count) checks, $($dtetResult.solveSeconds) s generation, $($dtetResult.historyBytes) history bytes."
