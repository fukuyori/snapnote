param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ProjectPath = Join-Path $RepoRoot "SnapNoteStudio.csproj"
$PublishDir = Join-Path $RepoRoot "bin\$Configuration\net8.0-windows\$Runtime\publish"
$ExePath = Join-Path $PublishDir "SnapNoteStudio.exe"

if ($Clean -and (Test-Path $PublishDir)) {
    Remove-Item -LiteralPath $PublishDir -Recurse -Force
}

dotnet restore $ProjectPath
dotnet publish $ProjectPath -c $Configuration -r $Runtime --self-contained true -o $PublishDir

if (-not (Test-Path $ExePath)) {
    throw "Release executable was not created: $ExePath"
}

Write-Host "Release build created:"
Write-Host $ExePath
