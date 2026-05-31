param(
    [switch]$SkipPublish,
    [string]$IsccPath
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ReleaseScript = Join-Path $PSScriptRoot "build-release.ps1"
$InstallerScript = Join-Path $RepoRoot "installer.iss"
$InstallerOutputDir = Join-Path $RepoRoot "installer_output"

if (-not $SkipPublish) {
    & $ReleaseScript
}

if ([string]::IsNullOrWhiteSpace($IsccPath)) {
    $command = Get-Command "iscc.exe" -ErrorAction SilentlyContinue
    if ($command) {
        $IsccPath = $command.Source
    }
}

if ([string]::IsNullOrWhiteSpace($IsccPath)) {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 5\ISCC.exe"
    )

    $IsccPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($IsccPath) -or -not (Test-Path $IsccPath)) {
    throw "Inno Setup compiler was not found. Install Inno Setup or pass -IsccPath `"C:\Path\To\ISCC.exe`"."
}

& $IsccPath $InstallerScript

if (-not (Test-Path $InstallerOutputDir)) {
    throw "Installer output directory was not created: $InstallerOutputDir"
}

Write-Host "Installer output:"
Get-ChildItem -Path $InstallerOutputDir -Filter "SnapNoteStudio_Setup_*.exe" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1 |
    ForEach-Object { Write-Host $_.FullName }
