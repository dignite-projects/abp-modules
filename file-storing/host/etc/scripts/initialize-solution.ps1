$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Run-Step {
    param(
        [string] $Name,
        [scriptblock] $Action
    )

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

Run-Step "Build" {
    Set-Location (Join-Path $scriptRoot "..\..\")
    dotnet build
}

Run-Step "InstallLibs" {
    Set-Location (Join-Path $scriptRoot "..\..\")
    abp install-libs
}

Run-Step "DevCert" {
    Set-Location (Join-Path $scriptRoot "../../Dignite.FileExplorer.Web.Host")
    dotnet dev-certs https -v -ep openiddict.pfx -p b495be51-7962-4bdd-acc3-694c070d50c9
}

exit 0
