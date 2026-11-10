$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path


function Write-StudioStep([string]$Message) {
    [Console]::Out.WriteLine("[STUDIO:STEP] $Message")
    [Console]::Out.Flush()
}

function Run-Step {
    param(
        [string] $Name,
        [string] $Message,
        [scriptblock] $Action
    )

    Write-StudioStep $Message

    try {
        & $Action

        if ($LASTEXITCODE -ne 0) {
            throw "Step '$Name' exited with code $LASTEXITCODE"
        }
    }
    catch {
        [Console]::Error.WriteLine("Step '$Name' FAILED")
        exit -1
    }
}

Run-Step "Build" "Building solution" {
    Set-Location (Join-Path $scriptRoot "..\..\..\")
    dotnet build Dignite.Drive.slnx
}

Run-Step "InstallLibs" "Installing client-side libraries" {
    Set-Location (Join-Path $scriptRoot "..\..\")
    abp install-libs
}

Run-Step "DbMigrator" "Running database migrator" {
    $projectDir = Join-Path $scriptRoot "../../Dignite.Drive.Host"
    # The CLI creates the migration and then runs the migrator. Do not start another
    # dotnet run until that process exits; a parallel run can seed before AbpSettings exists.
    if (-not (Test-Path (Join-Path $projectDir "Migrations"))) {
        & abp create-migration-and-run-migrator $projectDir --nolayers
    }
    else {
        Set-Location $projectDir
        dotnet run --migrate-database
    }
}

Run-Step "DevCert" "Creating development certificate" {
    Set-Location (Join-Path $scriptRoot "../../Dignite.Drive.Host")
    dotnet dev-certs https -v -ep openiddict.pfx -p 07934b3f-8326-4aa5-bc65-e3ecb94d5b38
}

exit 0
