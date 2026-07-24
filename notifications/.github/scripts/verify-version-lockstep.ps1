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
$angularPackage = Get-Content -Raw (Join-Path $repositoryRoot 'angular\projects\notification-center\package.json') |
    ConvertFrom-Json

if ($angularPackage.version -ne $dotnetVersion) {
    throw "NuGet version '$dotnetVersion' and Angular package version '$($angularPackage.version)' are not in lockstep."
}

if ($Tag) {
    $expectedTag = "v$dotnetVersion"
    if ($Tag -ne $expectedTag) {
        throw "Tag '$Tag' does not match release version '$expectedTag'."
    }
}

Write-Output $dotnetVersion
