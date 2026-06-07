# Build backend image locally for Qualcomm (ARM64), save to .tar.
# Sử dụng Docker Buildx để cross-build sang linux/arm64.
# Mặc định lưu thành file nén backend-qualcomm.tar.

param(
    [string]$ImageName = "nguyends/backend-qualcomm",
    [string]$Tag = "latest",
    [string]$TarFile = "backend-qualcomm.tar",
    [string]$Platform = "linux/amd64",
    [switch]$NoSave
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$FullImage = "${ImageName}:${Tag}"
Write-Host "Building Qualcomm ($Platform) Image: $FullImage" -ForegroundColor Cyan

# Kiểm tra Buildx
$buildxCheck = docker buildx version 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Docker Buildx is not available. Please enable Buildx to build images."
}

Write-Host "Building with Docker Buildx..." -ForegroundColor Gray
& docker buildx build --platform $Platform -t $FullImage --load -f Dockerfile .
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Build $Platform OK: $FullImage" -ForegroundColor Green

if (-not $NoSave) {
    $TarPath = Join-Path $PSScriptRoot $TarFile
    if (Test-Path $TarPath) { Remove-Item $TarPath }
    Write-Host "Saving image to $TarPath ..." -ForegroundColor Cyan
    & docker save $FullImage -o $TarPath
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Host "Saved successfully: $TarPath" -ForegroundColor Green
    Write-Host "Copy to server: scp $TarFile ai@100.101.128.127:~/nguyends/BKMeeting/" -ForegroundColor Green
}
