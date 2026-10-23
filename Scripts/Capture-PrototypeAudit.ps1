param(
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string[]]$Variants = @('Observe','Above','Side','Wrist','Options','Story','Graph','Model','Lensing','LensingOff','LensingRays','HudEnd')
)
$ErrorActionPreference = 'Stop'
$dtetRepo = Split-Path -Parent $PSScriptRoot
$dtetPlayer = Join-Path $dtetRepo 'BinaryBlackHole\Builds\VisualQA\BinaryBlackHole_VisualQA.exe'
if (!(Test-Path -LiteralPath $dtetPlayer)) { throw 'Build the QA player first with DT-ET > Validate Assets and Build QA Player.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$dtetOutput = (Resolve-Path -LiteralPath $OutputDirectory).Path
$dtetReceipts = @()
$dtetIndex = 0
foreach ($dtetVariant in $Variants) {
    $dtetIndex++
    $dtetName = '{0:D2}_{1}' -f $dtetIndex,$dtetVariant
    $dtetImage = Join-Path $dtetOutput ($dtetName+'.png')
    $dtetLog = Join-Path $dtetOutput ($dtetName+'.log')
    $env:DTET_CAPTURE_PATH = $dtetImage
    $env:DTET_CAPTURE_VARIANT = $dtetVariant
    try {
        $dtetProcess = Start-Process -FilePath $dtetPlayer -ArgumentList @('-screen-width','1600','-screen-height','900','-screen-fullscreen','0','-logFile',('"'+$dtetLog+'"')) -WindowStyle Hidden -PassThru
    } finally {
        Remove-Item Env:\DTET_CAPTURE_PATH,Env:\DTET_CAPTURE_VARIANT -ErrorAction SilentlyContinue
    }
    if (!$dtetProcess.WaitForExit(45000)) { throw "Owned QA player PID $($dtetProcess.Id) did not finish; inspect $dtetLog." }
    $dtetPasses = @(Select-String -LiteralPath $dtetLog -Pattern '^DTET_.*PASS$')
    $dtetFailures = @(Select-String -LiteralPath $dtetLog -Pattern '^DTET_.*FAILED|Exception:|^Shader error|error CS\d')
    if ($dtetProcess.ExitCode -ne 0 -or $dtetPasses.Count -ne 9 -or $dtetFailures.Count -ne 0 -or !(Test-Path -LiteralPath $dtetImage)) {
        throw "QA variant $dtetVariant failed: exit=$($dtetProcess.ExitCode), pass groups=$($dtetPasses.Count), failures=$($dtetFailures.Count). See $dtetLog."
    }
    $dtetPerformance = (Select-String -LiteralPath $dtetLog -Pattern '^DTET_PERFORMANCE').Line
    $dtetReceipts += [pscustomobject]@{ Variant=$dtetVariant; ExitCode=$dtetProcess.ExitCode; PassGroups=$dtetPasses.Count; Screenshot=$dtetImage; SHA256=(Get-FileHash -LiteralPath $dtetImage -Algorithm SHA256).Hash; Performance=$dtetPerformance }
    Write-Output "$dtetName PASS (9 groups) $dtetPerformance"
}
# Generated evidence, deliberately outside Unity Assets.
$dtetReceipts | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $dtetOutput 'capture-receipts.json') -Encoding utf8
