# Build backend image on LOCAL, save to .tar (no push).
# Then copy to server and: docker load -i meeting-backend.tar && docker run ...
param(
    [string]$ImageName = $env:IMAGE_NAME,
    [string]$Tag = $env:IMAGE_TAG,
    [string]$TarFile = "meeting-backend.tar"
)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
if (-not $ImageName) { $ImageName = "nguyendsbka/meeting-backend" }
if (-not $Tag) { $Tag = "latest" }
$FullImage = "${ImageName}:${Tag}"
Write-Host "Building: $FullImage" -ForegroundColor Cyan
& docker build -t $FullImage -f Dockerfile .
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Saving to $TarFile ..." -ForegroundColor Cyan
& docker save $FullImage -o $TarFile
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Done. Copy to server: scp $TarFile ubuntu@meeting.soict.io:~/meeting-deploy/backend/" -ForegroundColor Green
