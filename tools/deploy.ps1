# ============================================================================
# deploy.ps1 - One-click release: build + hash + manifest + upload
#
# Usage:
#   1) Copy deploy.config.sample.json to deploy.config.json and fill in your server
#   2) Edit tools\release-notes.txt (Chinese is fine - it is read as UTF-8)
#   3) powershell -File tools\deploy.ps1
#
#   Add -SkipUpload to only build locally (staging ends up in dist\upload\).
#
# This script publishes the APP only:
#   version.json + download/releases/<ver>.exe + download/SalaryTicker.exe symlink
#
# The website (index.html + assets/) has its own script: tools\deploy-site.ps1
#
# IMPORTANT: this file must stay ASCII-only.
#   Windows PowerShell 5.1 decodes .ps1 files without a BOM using the system
#   ANSI codepage (GBK on Chinese Windows). Non-ASCII characters here would be
#   mis-decoded and can corrupt string literals, breaking the script.
#   Data files it READS are still handled as UTF-8 explicitly (see below).
# ============================================================================

param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot "deploy.config.json"),
    [switch]$SkipUpload
)

$ErrorActionPreference = "Stop"

$root      = Split-Path -Parent $PSScriptRoot
$csproj    = Join-Path $root "SalaryTicker.csproj"
$notesFile = Join-Path $PSScriptRoot "release-notes.txt"
$distDir   = Join-Path $root "dist"
$exeName   = "SalaryTicker.exe"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " SalaryTicker - Release" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# ---------------------------------------------------------------- 0. tools
# Locate dotnet even when it is not on PATH (common when launched from a
# shell that does not inherit the Windows PATH).
$dotnet = "dotnet"
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $candidate = Join-Path $env:ProgramFiles "dotnet\dotnet.exe"
    if (Test-Path $candidate) { $dotnet = $candidate }
    else { throw "dotnet SDK not found. Please install the .NET 8 SDK first." }
}

# ssh / scp come from the Windows OpenSSH Client optional feature
foreach ($tool in @("ssh", "scp")) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        $openssh = Join-Path $env:SystemRoot "System32\OpenSSH\$tool.exe"
        if (-not (Test-Path $openssh)) {
            throw "$tool not found. Enable 'OpenSSH Client' in Windows optional features."
        }
    }
}

# ---------------------------------------------------------------- 1. version
# Read as UTF-8 explicitly: the csproj contains Chinese comments, and
# Get-Content without -Encoding would decode them with the ANSI codepage.
$csprojText = Get-Content $csproj -Raw -Encoding UTF8
if ($csprojText -notmatch '<Version>([^<]+)</Version>') {
    throw "Cannot read <Version> from SalaryTicker.csproj"
}
$version = $Matches[1].Trim()
Write-Host "[1/6] Version: $version" -ForegroundColor Yellow

# ---------------------------------------------------------------- 2. publish
Write-Host "[2/6] Publishing (self-contained single file, 1-3 min)..." -ForegroundColor Yellow
if (Test-Path $distDir) { Remove-Item $distDir -Recurse -Force }

Push-Location $root
try {
    & $dotnet publish $csproj `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $distDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }
}
finally { Pop-Location }

$exePath = Join-Path $distDir $exeName
if (-not (Test-Path $exePath)) { throw "Publish output not found: $exePath" }

# ---------------------------------------------------------------- 3. hash
Write-Host "[3/6] Computing SHA256..." -ForegroundColor Yellow
$hash   = (Get-FileHash $exePath -Algorithm SHA256).Hash
$size   = (Get-Item $exePath).Length
$sizeMB = [math]::Round($size / 1MB, 1)
Write-Host "      SHA256: $hash"
Write-Host "      Size  : $sizeMB MB"

# ---------------------------------------------------------------- 4. manifest
# Release notes may contain Chinese - read them as UTF-8.
$notes = if (Test-Path $notesFile) { (Get-Content $notesFile -Raw -Encoding UTF8).Trim() } else { "" }
if (-not $notes) { $notes = "Version $version" }

$cfg = $null
if (Test-Path $ConfigPath) {
    $cfg = Get-Content $ConfigPath -Raw | ConvertFrom-Json
}
$publicUrl = if ($cfg -and $cfg.publicUrl) { $cfg.publicUrl.TrimEnd('/') } else { "" }

$archiveName = "SalaryTicker-$version.exe"
$downloadUrl = if ($publicUrl) { "$publicUrl/download/releases/$archiveName" } else { "download/releases/$archiveName" }

$manifest = [ordered]@{
    version      = $version
    url          = $downloadUrl
    sha256       = $hash
    size         = $size
    siteUrl      = if ($publicUrl) { "$publicUrl/" } else { "" }
    releaseNotes = $notes
    publishedAt  = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
}
$manifestJson = $manifest | ConvertTo-Json -Depth 4

# Stage upload-ready files
$stageDir = Join-Path $distDir "upload"
New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
Copy-Item $exePath (Join-Path $stageDir $archiveName) -Force

# Write version.json as UTF-8 WITHOUT BOM:
# PS 5.1 "Set-Content -Encoding UTF8" adds a BOM, which can break the
# System.Text.Json parser on the client side.
$manifestPath = Join-Path $stageDir "version.json"
[System.IO.File]::WriteAllText($manifestPath, $manifestJson, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "[4/6] Manifest generated:" -ForegroundColor Yellow
Write-Host "      $downloadUrl"

if ($SkipUpload -or -not $cfg) {
    if (-not $cfg -and -not $SkipUpload) {
        Write-Host "[5/6] deploy.config.json not found - skipping upload" -ForegroundColor DarkYellow
        Write-Host "      Copy deploy.config.sample.json to deploy.config.json first." -ForegroundColor DarkYellow
    } else {
        Write-Host "[5/6] -SkipUpload specified" -ForegroundColor DarkYellow
    }
    Write-Host ""
    Write-Host "Local artifacts:" -ForegroundColor Green
    Write-Host "  $(Join-Path $stageDir $archiveName)"
    Write-Host "  $manifestPath"
    exit 0
}

# ---------------------------------------------------------------- 5. upload
Write-Host "[5/6] Uploading to $($cfg.user)@$($cfg.host):$($cfg.remoteDir) ..." -ForegroundColor Yellow

$sshArgs = @("-P", "$($cfg.port)")
$scpArgs = @("-P", "$($cfg.port)")
if ($cfg.keyPath) {
    if (Test-Path $cfg.keyPath) {
        $sshArgs += @("-i", $cfg.keyPath)
        $scpArgs += @("-i", $cfg.keyPath)
    } else {
        # Placeholder or stale path in deploy.config.json - fall back to the
        # default key (~/.ssh/id_rsa) instead of failing with "identity file not found"
        Write-Host "      note: keyPath not found ('$($cfg.keyPath)'), using default ssh key" -ForegroundColor DarkYellow
    }
}

$target = "$($cfg.user)@$($cfg.host)"
$remoteDir = $cfg.remoteDir.TrimEnd('/')

# Layout on the server:
#   <remoteDir>/version.json                     -> app checks this
#   <remoteDir>/download/SalaryTicker.exe        -> clean download path (symlink)
#   <remoteDir>/download/releases/<ver>.exe      -> versioned archive
& ssh @sshArgs $target "mkdir -p '$remoteDir/download/releases'"
if ($LASTEXITCODE -ne 0) { throw "ssh failed - check host/user/key in deploy.config.json" }

& scp @scpArgs (Join-Path $stageDir $archiveName) "${target}:$remoteDir/download/releases/"
if ($LASTEXITCODE -ne 0) { throw "scp failed (archive)" }

& scp @scpArgs $manifestPath "${target}:$remoteDir/version.json"
if ($LASTEXITCODE -ne 0) { throw "scp failed (version.json)" }

& ssh @sshArgs $target "cd '$remoteDir/download' && ln -sfn 'releases/$archiveName' $exeName && ls -l $exeName"
if ($LASTEXITCODE -ne 0) { throw "ssh failed (symlink)" }

# ---------------------------------------------------------------- 6. summary
Write-Host "[6/6] Done." -ForegroundColor Green
Write-Host ""
Write-Host "Version : $version"
Write-Host "Size    : $sizeMB MB"
Write-Host "SHA256  : $hash"
if ($publicUrl) {
    Write-Host "Download: $publicUrl/download/$exeName" -ForegroundColor Cyan
    Write-Host "Archive : $downloadUrl" -ForegroundColor Cyan
    Write-Host "Manifest: $publicUrl/version.json" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "(website is published separately: tools\deploy-site.ps1)" -ForegroundColor DarkGray
}
