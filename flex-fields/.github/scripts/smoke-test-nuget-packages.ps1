param(
    [Parameter(Mandatory = $true)]
    [string] $ArtifactsPath,

    [Parameter(Mandatory = $true)]
    [string] $Version
)

$ErrorActionPreference = 'Stop'

$artifacts = (Resolve-Path -LiteralPath $ArtifactsPath).Path
$packageIds = @(
    'Dignite.Abp.FlexFields.Domain.Shared',
    'Dignite.Abp.FlexFields.Abstractions',
    'Dignite.Abp.FlexFields.Domain',
    'Dignite.Abp.FlexFields.EntityFrameworkCore',
    'Dignite.Abp.FlexFields.MongoDB',
    'Dignite.Abp.FlexFields.Installer'
)

foreach ($packageId in $packageIds) {
    $packagePath = Join-Path $artifacts "$packageId.$Version.nupkg"
    if (-not (Test-Path -LiteralPath $packagePath)) {
        throw "Expected package was not produced: $packagePath"
    }
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "dignite-flex-fields-nuget-smoke-$([Guid]::NewGuid().ToString('N'))"
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

    Set-Content -LiteralPath $sourcePath -Encoding utf8 -Value @'
namespace PackageSmoke;

public static class PackageSurface
{
    public static readonly Type[] ModuleTypes =
    [
        typeof(Dignite.Abp.FlexFields.FlexFieldsDomainSharedModule),
        typeof(Dignite.Abp.FlexFields.FlexFieldsAbstractionsModule),
        typeof(Dignite.Abp.FlexFields.FlexFieldsDomainModule),
        typeof(Dignite.Abp.FlexFields.EntityFrameworkCore.FlexFieldsEntityFrameworkCoreModule),
        typeof(Dignite.Abp.FlexFields.MongoDB.FlexFieldsMongoDbModule),
        typeof(Dignite.Abp.FlexFields.FlexFieldsInstallerModule)
    ];
}
'@

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

    Write-Host "Successfully restored and compiled a consumer of all $($packageIds.Count) FlexFields NuGet packages at version $Version."
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
