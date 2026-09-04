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

# Every package above ships from this repository at one lockstep version, so a dependency between
# any two of them must name that same version. Left to drift, an adapter goes on declaring a range
# wide enough to admit an older sibling -- "^10.0.0-rc.4" long after everything ships 10.0.0-rc.13,
# say -- and a consumer's resolver is free to satisfy it with that older sibling rather than
# deduplicating against the copy already at the root. That is not a wasted-bytes problem. Angular DI
# keys off object identity, and FLEX_FIELD_TYPES is a module-scoped InjectionToken, so two copies of
# @dignite/ng.flex-fields are two distinct DI keys: provideCKEditorFieldType() registers into one
# while FieldTypeResolver reads the other, and every field type looks unregistered at runtime with
# nothing having failed at install or build time. Yarn Classic reaches exactly that resolution
# whenever npm's latest tag sits on an older version than the newest published one. See issue #211.
$expectedRange = "^$dotnetVersion"

foreach ($relativePath in $angularPackages) {
    $packagePath = Join-Path $repositoryRoot $relativePath
    $package = Get-Content -Raw $packagePath | ConvertFrom-Json
    if ($package.version -ne $dotnetVersion) {
        throw "NuGet version '$dotnetVersion' and Angular package '$($package.name)' version '$($package.version)' ($relativePath) are not in lockstep."
    }

    foreach ($section in 'dependencies', 'peerDependencies') {
        $dependencies = $package.$section
        if (-not $dependencies) {
            continue
        }

        foreach ($dependency in $dependencies.PSObject.Properties) {
            if ($dependency.Name -notlike '@dignite/*') {
                continue
            }

            if ($dependency.Value -ne $expectedRange) {
                throw "Angular package '$($package.name)' ($relativePath) declares $section '$($dependency.Name)': '$($dependency.Value)', but a dependency on another package from this repository must track the release version ('$expectedRange'). See issue #211."
            }
        }
    }
}

if ($Tag) {
    $expectedTag = "v$dotnetVersion"
    if ($Tag -ne $expectedTag) {
        throw "Tag '$Tag' does not match release version '$expectedTag'."
    }
}

Write-Output $dotnetVersion
