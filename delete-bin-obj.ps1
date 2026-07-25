# Recursively deletes every bin/ and obj/ folder under the repo, skipping node_modules (the Angular
# workspaces under file-storing/angular and notifications/angular have their own bin/obj-named
# dependencies that must not be touched). Handy when a stale build or a provider swap leaves the
# incremental build in a bad state. Adapted from the ABP Framework's delete-bin-obj.ps1.
Clear-Host
Write-Host "Deleting all BIN and OBJ folders..." -ForegroundColor Cyan
Get-ChildItem -Path $PSScriptRoot -Include bin,obj -Recurse -Directory | ForEach-Object {
    if ($_.FullName -notmatch "\\node_modules\\") {
        Write-Host "Deleting:" $_.FullName -ForegroundColor Yellow
        Remove-Item $_.FullName -Recurse -Force
    } else {
        Write-Host "Skipping:" $_.FullName -ForegroundColor Magenta
    }
}

Write-Host "BIN and OBJ folders have been successfully deleted." -ForegroundColor Green
