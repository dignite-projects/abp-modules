param(
    [string] $Tag = ''
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$buildProps = Get-Content -Raw (Join-Path $repositoryRoot 'Directory.Build.props')
$versionMatch = [regex]::Match($buildProps, '<Version>([^<]+)</Version>')
if (-not $versionMatch.Success) {
    throw 'Could not read <Version> from Directory.Build.props.'
}

$dotnetVersion = $versionMatch.Groups[1].Value

$angularPackages = @(
    'file-storing\angular\projects\file-explorer\package.json',
    'notifications\angular\projects\notification-center\package.json',
    'flex-fields\angular\projects\flex-fields\package.json',
    'flex-fields\angular\projects\flex-fields-file-explorer\package.json',
    'flex-fields\angular\projects\flex-fields-ckeditor\package.json'
)

foreach ($relativePath in $angularPackages) {
    $packagePath = Join-Path $repositoryRoot $relativePath
    $package = Get-Content -Raw $packagePath | ConvertFrom-Json
    if ($package.version -ne $dotnetVersion) {
        throw "NuGet version '$dotnetVersion' and Angular package '$($package.name)' version '$($package.version)' ($relativePath) are not in lockstep."
    }
}

if ($Tag) {
    $expectedTag = "v$dotnetVersion"
    if ($Tag -ne $expectedTag) {
        throw "Tag '$Tag' does not match release version '$expectedTag'."
    }
}

Write-Output $dotnetVersion
