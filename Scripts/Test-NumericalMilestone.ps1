param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$dtetRepo = Split-Path -Parent $PSScriptRoot
$dtetPlayer = Join-Path $dtetRepo 'BinaryBlackHole\Builds\VisualQA\BinaryBlackHole_VisualQA.exe'
if (!(Test-Path -LiteralPath $dtetPlayer)) { throw 'Build the development QA player with DT-ET > Validate Assets and Build QA Player.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$dtetOutput = (Resolve-Path -LiteralPath $OutputDirectory).Path
$dtetLog = Join-Path $dtetOutput 'player.log'
$dtetReceiptPath = Join-Path $dtetOutput 'numerical-integration.json'
$dtetEnvironmentNames = @('DTET_NUMERICAL_QA_PATH','DTET_PHYSICS_QA_PATH','DTET_CAPTURE_PATH','DTET_CAPTURE_VARIANT')
$dtetPreviousEnvironment = @{}
foreach ($dtetName in $dtetEnvironmentNames) {
    $dtetPreviousEnvironment[$dtetName] = [Environment]::GetEnvironmentVariable($dtetName, 'Process')
}
$dtetStarted = [DateTime]::UtcNow
try {
    foreach ($dtetName in $dtetEnvironmentNames) { [Environment]::SetEnvironmentVariable($dtetName, $null, 'Process') }
    [Environment]::SetEnvironmentVariable('DTET_NUMERICAL_QA_PATH', $dtetOutput, 'Process')
    $dtetProcess = Start-Process -FilePath $dtetPlayer -ArgumentList @('-screen-width','1600','-screen-height','900','-screen-fullscreen','0','-logFile',('"'+$dtetLog+'"')) -WindowStyle Hidden -PassThru
} finally {
    foreach ($dtetName in $dtetEnvironmentNames) {
        [Environment]::SetEnvironmentVariable($dtetName, $dtetPreviousEnvironment[$dtetName], 'Process')
    }
}
if (!$dtetProcess.WaitForExit(45000)) { throw "Owned numerical QA process $($dtetProcess.Id) did not finish. Inspect $dtetLog." }
if ($dtetProcess.ExitCode -ne 0 -or !(Test-Path -LiteralPath $dtetReceiptPath)) { throw "Numerical integration failed: exit $($dtetProcess.ExitCode). Inspect $dtetLog." }
if ((Get-Item -LiteralPath $dtetReceiptPath).LastWriteTimeUtc -lt $dtetStarted) { throw "Numerical integration receipt was not written by this run. Inspect $dtetLog." }
$dtetReceipt = Get-Content -LiteralPath $dtetReceiptPath -Raw | ConvertFrom-Json
$dtetFailures = @(Select-String -LiteralPath $dtetLog -Pattern 'Exception:|DTET_NUMERICAL_FAILED|DTET_NUMERICAL_INTEGRATION_FAILED|^Shader error|error CS\d')
$dtetPasses = @(Select-String -LiteralPath $dtetLog -Pattern '^DTET_NUMERICAL_INTEGRATION_PASS$')
if (!$dtetReceipt.success -or $dtetFailures.Count -gt 0 -or $dtetPasses.Count -ne 1) { throw "Numerical integration failed. Inspect $dtetLog." }
Write-Output "PASS numerical player integration: $($dtetReceipt.checks.Count) checks; $($dtetReceipt.graphicsDevice); $dtetReceiptPath"
