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

# MAJOR.MINOR of the release version is the ABP Framework MAJOR.MINOR it is built against, so a
# developer reading "10.7.x" knows it targets ABP 10.7 - see CONTRIBUTING.md#versioning-and-releases.
# Volo.Abp.Core stands in for every Volo.Abp.* pin in Directory.Packages.props (they move together).
$packagesProps = Get-Content -Raw (Join-Path $repositoryRoot 'Directory.Packages.props')
$abpMatch = [regex]::Match($packagesProps, '<PackageVersion\s+Include="Volo\.Abp\.Core"\s+Version="([^"]+)"')
if (-not $abpMatch.Success) {
    throw 'Could not read the Volo.Abp.Core version from Directory.Packages.props.'
}

$abpVersion = $abpMatch.Groups[1].Value
$majorMinor = { param($v) ([regex]::Match($v, '^\d+\.\d+')).Value }
$abpMajorMinor = & $majorMinor $abpVersion
if ((& $majorMinor $dotnetVersion) -ne $abpMajorMinor) {
    throw "Release version '$dotnetVersion' must start with the ABP Framework MAJOR.MINOR it targets ('$abpMajorMinor.', from Volo.Abp.Core $abpVersion in Directory.Packages.props)."
}

$angularPackages = @(
    'notifications\angular\projects\notification-center\package.json',
    'flex-fields\angular\projects\flex-fields\package.json',
    'flex-fields\angular\projects\flex-fields-ckeditor\package.json'
)

# Every package above ships from this repository at one lockstep version, so a dependency between
# any two of them must pin that exact version -- never a "^" range. These packages can never diverge
# from each other (they always release together, at the same version, from the same commit), so there
# is no second, still-compatible version a range would ever need to admit. What a range used to buy
# instead was risk: one wide enough to admit an older sibling -- "^10.0.0-rc.4" long after everything
# ships 10.0.0-rc.13, say -- lets a consumer's resolver satisfy it with that older sibling rather than
# deduplicating against the copy already at the root. That is not a wasted-bytes problem. Angular DI
# keys off object identity, and FLEX_FIELD_TYPES is a module-scoped InjectionToken, so two copies of
# @dignite/ng.flex-fields are two distinct DI keys: provideCKEditorFieldType() registers into one
# while FieldTypeResolver reads the other, and every field type looks unregistered at runtime with
# nothing having failed at install or build time. Yarn Classic reaches exactly that resolution
# whenever npm's latest tag sits on an older version than the newest published one. See issue #211.
# An exact pin removes the range entirely, so there is nothing left for a resolver to misjudge.
$expectedVersionString = $dotnetVersion

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
            # The version promises one ABP minor line, so the npm range must admit exactly that line
            # ("~10.7.0"), not the next minor a "^" range would let in.
            if ($dependency.Name -like '@abp/ng.*' -and -not $dependency.Value.StartsWith("~$abpMajorMinor.")) {
                throw "Angular package '$($package.name)' ($relativePath) declares $section '$($dependency.Name)': '$($dependency.Value)', but it must be a '~$abpMajorMinor.x' range to match the ABP line this release targets."
            }

            if ($dependency.Name -notlike '@dignite/*') {
                continue
            }

            if ($dependency.Value -ne $expectedVersionString) {
                throw "Angular package '$($package.name)' ($relativePath) declares $section '$($dependency.Name)': '$($dependency.Value)', but a dependency on another package from this repository must pin the exact release version ('$expectedVersionString'), not a range. See issue #211."
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
