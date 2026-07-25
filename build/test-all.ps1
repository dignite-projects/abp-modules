# Runs the whole test suite across both module trees via the aggregate solution. Test projects
# without a runner (the shared *.TestBase projects) are skipped automatically. The MongoDB provider
# tests spin up an embedded mongod via MongoSandbox, so no external MongoDB is required.
#
#   ./build/test-all.ps1              # run all tests (Release)
#   ./build/test-all.ps1 -Coverage   # also collect coverage via coverlet (see codecov.yml)
param(
    [switch]$Coverage,
    [string]$Configuration = "Release"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root "Dignite.Abp.Modules.slnx"

$dotnetArgs = @("test", $solution, "--configuration", $Configuration)
if ($Coverage) {
    $dotnetArgs += @("--collect:XPlat Code Coverage")
    Write-Host "Running tests with code coverage ($Configuration)..." -ForegroundColor Cyan
} else {
    Write-Host "Running tests ($Configuration)..." -ForegroundColor Cyan
}

dotnet @dotnetArgs
if ($LASTEXITCODE -ne 0) { throw "Tests failed with exit code $LASTEXITCODE." }
Write-Host "All tests passed." -ForegroundColor Green
