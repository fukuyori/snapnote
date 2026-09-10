<#
.SYNOPSIS
Publish Snapnote as a self-contained single executable.

.PARAMETER NoUiAccess
By default the uiAccess manifest is embedded so the installed, signed release can receive
capture hotkeys while elevated applications are active. Such an executable only starts when
it is Authenticode-signed with a machine-trusted certificate and installed under Program Files.
-NoUiAccess creates a plain executable for local testing.
#>
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$Clean,
    [switch]$NoUiAccess
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ProjectPath = Join-Path $RepoRoot "SnapNoteStudio.csproj"
$PublishDir = Join-Path $RepoRoot "bin\$Configuration\net8.0-windows10.0.19041.0\$Runtime\publish"
$ExePath = Join-Path $PublishDir "SnapNoteStudio.exe"

if ($Clean -and (Test-Path $PublishDir)) {
    Remove-Item -LiteralPath $PublishDir -Recurse -Force
}

dotnet restore $ProjectPath
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed ($LASTEXITCODE)." }

$publishArgs = @(
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", "true",
    "-o", $PublishDir
)

if ($NoUiAccess) {
    Write-Host "uiAccess manifest disabled (-NoUiAccess): plain executable for local testing."
    $publishArgs += "-p:UiAccess=false"
} else {
    Write-Host "uiAccess manifest enabled: the executable must be signed and installed under Program Files."
    $publishArgs += "-p:UiAccess=true"
}

dotnet publish $ProjectPath @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)." }

if (-not (Test-Path $ExePath)) {
    throw "Release executable was not created: $ExePath"
}

Write-Host "Release build created:"
Write-Host $ExePath
