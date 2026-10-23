# Optional automated evidence collection; ordinary launchers do not run tests.
param([switch]$PerformanceOnly)
$ErrorActionPreference = 'Stop'
try {
    $label = if ($PerformanceOnly) { 'PerformanceCheck' } else { 'DeviceCheck' }
    $name = $label + '_' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '_' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $output = Join-Path $PSScriptRoot $name
    if (Test-Path -LiteralPath $output) { throw "Refusing to overwrite: $output" }
    Write-Output "Evidence output: $output"
    & (Join-Path $PSScriptRoot 'Run-DeviceCheck.ps1') -OutputDirectory $output -BenchmarkSeconds 5 -PerformanceOnly:$PerformanceOnly
    exit 0
} catch {
    Write-Error $_
    exit 1
}
