$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'tests\PixelationChecks\PixelationChecks.csproj'
dotnet run --project $project -c Release -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw "Pixelation checks failed ($LASTEXITCODE)." }
