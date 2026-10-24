param(
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [Parameter(Mandatory = $true)][string]$VideoPath
)
$ErrorActionPreference = 'Stop'
$dtetRepo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$dtetBuild = Join-Path $dtetRepo 'BinaryBlackHole\Builds\Submission'
$dtetTemplates = Join-Path $dtetRepo 'Packaging\Demo'
$dtetVideo = (Resolve-Path -LiteralPath $VideoPath).Path
$dtetOutput = [IO.Path]::GetFullPath($OutputDirectory)
$dtetPackage = Join-Path $dtetOutput 'DTET_Prototype_Demo'
$dtetZip = Join-Path $dtetOutput 'DTET_Prototype_Demo_Windows.zip'
if ((Test-Path -LiteralPath $dtetPackage) -or (Test-Path -LiteralPath $dtetZip)) {
    throw 'Package target already exists. Choose a new output directory to preserve previous work.'
}
foreach ($dtetRequired in @('DTET_Prototype.exe', 'UnityPlayer.dll', 'DTET_Prototype_Data', 'MonoBleedingEdge')) {
    if (!(Test-Path -LiteralPath (Join-Path $dtetBuild $dtetRequired))) {
        throw "Required release-build component missing: $dtetRequired"
    }
}
New-Item -ItemType Directory -Path $dtetPackage -Force | Out-Null
Copy-Item -Path (Join-Path $dtetTemplates '*') -Destination $dtetPackage -Recurse
$dtetRuntime = Join-Path $dtetPackage 'Demo'
New-Item -ItemType Directory -Path $dtetRuntime | Out-Null
# Allow only release-player roots; preserve dependencies except symbols and backups.
$dtetAllowedRoots = @('DTET_Prototype.exe','UnityPlayer.dll','UnityCrashHandler64.exe',
    'DTET_Prototype_Data','MonoBleedingEdge','D3D12','WinPixEventRuntime.dll')
foreach ($dtetFile in Get-ChildItem -LiteralPath $dtetBuild -File -Recurse) {
    $dtetRelative = [IO.Path]::GetRelativePath($dtetBuild, $dtetFile.FullName)
    $dtetRoot = ($dtetRelative -split '[\\/]')[0]
    if ($dtetRoot -notin $dtetAllowedRoots) { continue }
    if ($dtetRelative -match '(?i)DoNotShip|\.(pdb|mdb|bak|orig|log)$') { continue }
    $dtetDestination = Join-Path $dtetRuntime $dtetRelative
    New-Item -ItemType Directory -Path (Split-Path $dtetDestination -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $dtetFile.FullName -Destination $dtetDestination
}
New-Item -ItemType Directory -Path (Join-Path $dtetPackage 'Video') | Out-Null
Copy-Item -LiteralPath $dtetVideo -Destination (Join-Path $dtetPackage 'Video\DTET_Prototype_Demo.mp4')
$dtetManifest = foreach ($dtetFile in Get-ChildItem -LiteralPath $dtetPackage -File -Recurse) {
    [pscustomobject]@{
        Path = [IO.Path]::GetRelativePath($dtetPackage, $dtetFile.FullName)
        Bytes = $dtetFile.Length
        SHA256 = (Get-FileHash -LiteralPath $dtetFile.FullName -Algorithm SHA256).Hash
    }
}
# Verification records stay outside the submission folder.
$dtetManifest | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $dtetOutput 'package-manifest.json')
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($dtetPackage, $dtetZip, [IO.Compression.CompressionLevel]::Optimal, $true)
$dtetArchive = [IO.Compression.ZipFile]::OpenRead($dtetZip)
try {
    if ($dtetArchive.Entries.Count -ne @($dtetManifest).Count) { throw 'ZIP entry count differs from staged package' }
    foreach ($dtetEntry in $dtetArchive.Entries) {
        if ($dtetEntry.FullName -match '(?i)DoNotShip|(^|/)(Library|Temp|obj|\.git)/|\.(pdb|mdb|bak|orig|log)$') {
            throw "Unwanted archive entry: $($dtetEntry.FullName)"
        }
    }
} finally { $dtetArchive.Dispose() }
Write-Output $dtetZip
