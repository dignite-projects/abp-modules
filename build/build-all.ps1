# Debug build of the whole monorepo (both module trees + the two demo hosts) via the aggregate
# solution. Run from anywhere: `./build/build-all.ps1`.
# For a production-shaped build (Release, which is where ConfigureAwait.Fody weaving kicks in) use
# build-all-release.ps1; to run the tests use test-all.ps1.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root "Dignite.Abp.Modules.slnx"

Write-Host "Building $solution (Debug)..." -ForegroundColor Cyan
dotnet build $solution --configuration Debug
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }
Write-Host "Build succeeded." -ForegroundColor Green
