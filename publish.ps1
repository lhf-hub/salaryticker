# ============================================================================
# publish.ps1 - Build a self-contained single-file exe for SalaryTicker
#
# Usage: powershell -ExecutionPolicy Bypass -File publish.ps1
#
# Notes:
#   - WPF does NOT support IL trimming (PublishTrimmed), so it is omitted.
#   - Self-contained output is ~70-100 MB (includes .NET runtime) - normal.
#   - Output directory: publish/
#   - This script uses ASCII only to avoid PowerShell 5.1 encoding issues.
# ============================================================================

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " SalaryTicker - Publish Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$projectDir = $PSScriptRoot
$outputDir = Join-Path $projectDir "publish"

if (Test-Path $outputDir) {
    Write-Host "[1/3] Cleaning old publish output..." -ForegroundColor Yellow
    Remove-Item -Path $outputDir -Recurse -Force
} else {
    Write-Host "[1/3] Nothing to clean" -ForegroundColor Yellow
}

Write-Host "[2/3] Publishing (self-contained single file, 1-3 minutes)..." -ForegroundColor Yellow
Push-Location $projectDir
try {
    dotnet publish SalaryTicker.csproj `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $outputDir

    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

Write-Host "[3/3] Done." -ForegroundColor Green
Write-Host ""

$exe = Join-Path $outputDir "SalaryTicker.exe"
if (Test-Path $exe) {
    $sizeMB = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Host "Output : $exe" -ForegroundColor Green
    Write-Host "Size   : $sizeMB MB" -ForegroundColor Green
    Write-Host ""
    Write-Host "Double-click the exe to run. No .NET runtime installation required." -ForegroundColor Cyan
    Write-Host "Config data is stored in: %AppData%\SalaryTicker\" -ForegroundColor Cyan
} else {
    Write-Host "WARNING: SalaryTicker.exe not found" -ForegroundColor Red
}
