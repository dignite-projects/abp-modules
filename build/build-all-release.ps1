# Release build of the whole monorepo via the aggregate solution. This is the configuration the CI
# workflow builds and packs, and the only one in which ConfigureAwait.Fody rewrites awaits in the
# packable libraries (see Directory.Build.props). Run from anywhere: `./build/build-all-release.ps1`.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root "Dignite.Abp.Modules.slnx"

Write-Host "Building $solution (Release)..." -ForegroundColor Cyan
dotnet build $solution --configuration Release
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }
Write-Host "Release build succeeded." -ForegroundColor Green
