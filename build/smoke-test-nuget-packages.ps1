<#
.SYNOPSIS
    Restores and compiles a throwaway consumer of every NuGet package one module ships, from the
    freshly packed artifacts, to catch a package that packs but cannot actually be consumed.

.DESCRIPTION
    The per-module package list and the public types the consumer touches live in
    <Module>/.github/scripts/nuget-surface.psd1 (PackageIds, Types, optional Label), so adding a
    package to a module means editing that data file, not this script.

.EXAMPLE
    ./build/smoke-test-nuget-packages.ps1 ./artifacts-ci/nuget 10.0.0-rc.28 -Module notifications
#>
param(
    [Parameter(Mandatory = $true)]
    [string] $ArtifactsPath,

    [Parameter(Mandatory = $true)]
    [string] $Version,

    [Parameter(Mandatory = $true)]
    [ValidateSet('aspnetcore-mcp', 'blob-storing', 'flex-fields', 'notifications')]
    [string] $Module
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$surfacePath = Join-Path $repoRoot "$Module/.github/scripts/nuget-surface.psd1"
$surface = Import-PowerShellDataFile -LiteralPath $surfacePath
$packageIds = @($surface.PackageIds)
$types = @($surface.Types)
$label = if ($surface.ContainsKey('Label')) { $surface.Label } else { '' }

$artifacts = (Resolve-Path -LiteralPath $ArtifactsPath).Path

foreach ($packageId in $packageIds) {
    $packagePath = Join-Path $artifacts "$packageId.$Version.nupkg"
    if (-not (Test-Path -LiteralPath $packagePath)) {
        throw "Expected package was not produced: $packagePath"
    }
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "dignite-$Module-nuget-smoke-$([Guid]::NewGuid().ToString('N'))"
$projectPath = Join-Path $tempRoot 'PackageSmoke.csproj'
$sourcePath = Join-Path $tempRoot 'PackageSmoke.cs'
$nuGetConfigPath = Join-Path $tempRoot 'NuGet.Config'
$previousNuGetPackages = $env:NUGET_PACKAGES

try {
    New-Item -ItemType Directory -Path $tempRoot | Out-Null
    $env:NUGET_PACKAGES = Join-Path $tempRoot '.nuget-packages'

    $packageReferences = ($packageIds | ForEach-Object {
        "    <PackageReference Include=`"$_`" Version=`"$Version`" />"
    }) -join [Environment]::NewLine

    Set-Content -LiteralPath $projectPath -Encoding utf8 -Value @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
$packageReferences
  </ItemGroup>
</Project>
"@

    $typeofs = ($types | ForEach-Object { "        typeof($_)" }) -join (',' + [Environment]::NewLine)

    Set-Content -LiteralPath $sourcePath -Encoding utf8 -Value @"
namespace PackageSmoke;

public static class PackageSurface
{
    public static readonly Type[] ModuleTypes =
    [
$typeofs
    ];
}
"@

    $escapedArtifacts = [System.Security.SecurityElement]::Escape($artifacts)
    Set-Content -LiteralPath $nuGetConfigPath -Encoding utf8 -Value @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="package-smoke" value="$escapedArtifacts" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
"@

    & dotnet restore $projectPath --configfile $nuGetConfigPath
    if ($LASTEXITCODE -ne 0) {
        throw "NuGet package smoke restore failed with exit code $LASTEXITCODE."
    }

    & dotnet build $projectPath --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "NuGet package smoke build failed with exit code $LASTEXITCODE."
    }

    Write-Host "Successfully restored and compiled a consumer of all $($packageIds.Count) ${label}NuGet packages at version $Version."
}
finally {
    if ($null -eq $previousNuGetPackages) {
        Remove-Item Env:NUGET_PACKAGES -ErrorAction SilentlyContinue
    }
    else {
        $env:NUGET_PACKAGES = $previousNuGetPackages
    }

    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
