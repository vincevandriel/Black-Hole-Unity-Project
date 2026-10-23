param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$dtetRepo = Split-Path -Parent $PSScriptRoot
$dtetPlayer = Join-Path $dtetRepo 'BinaryBlackHole\Builds\VisualQA\BinaryBlackHole_VisualQA.exe'
if (!(Test-Path -LiteralPath $dtetPlayer)) { throw 'Build the development QA player with DT-ET > Validate Assets and Build QA Player.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$dtetOutput = (Resolve-Path -LiteralPath $OutputDirectory).Path
$dtetLog = Join-Path $dtetOutput 'player.log'
$env:DTET_PHYSICS_QA_PATH = $dtetOutput
try {
    $dtetProcess = Start-Process -FilePath $dtetPlayer -ArgumentList @('-screen-width','1600','-screen-height','900','-screen-fullscreen','0','-logFile',('"'+$dtetLog+'"')) -WindowStyle Hidden -PassThru
} finally {
    Remove-Item Env:\DTET_PHYSICS_QA_PATH -ErrorAction SilentlyContinue
}
if (!$dtetProcess.WaitForExit(45000)) { throw "Owned QA process $($dtetProcess.Id) did not finish. Inspect $dtetLog." }
$dtetReceiptPath = Join-Path $dtetOutput 'physical-integration.json'
if ($dtetProcess.ExitCode -ne 0 -or !(Test-Path -LiteralPath $dtetReceiptPath)) { throw "Physical integration failed: exit $($dtetProcess.ExitCode). Inspect $dtetLog." }
$dtetReceipt = Get-Content -LiteralPath $dtetReceiptPath -Raw | ConvertFrom-Json
if (!$dtetReceipt.success -or @(Select-String -LiteralPath $dtetLog -Pattern 'Exception:|DTET_PHYSICS_FAILED|^Shader error|error CS\d').Count -gt 0) { throw "Physical integration failed. Inspect $dtetLog." }
Write-Output "PASS physical player integration: $($dtetReceipt.checks.Count) checks; $($dtetReceipt.graphicsDevice); $dtetReceiptPath"
