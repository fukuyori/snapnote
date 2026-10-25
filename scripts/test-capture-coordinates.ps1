param([switch]$Live)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'tests\CaptureCoordinateChecks\CaptureCoordinateChecks.csproj'
$runArgs = @('run', '--project', $project, '-c', 'Release', '-p:NuGetAudit=false')
if ($Live) { $runArgs += @('--', '--live') }
dotnet @runArgs
if ($LASTEXITCODE -ne 0) { throw "Capture coordinate checks failed ($LASTEXITCODE)." }
