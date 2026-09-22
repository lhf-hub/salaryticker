# ============================================================================
# deploy-site.ps1 - One-click website release (index.html + assets/)
#
# Usage:
#   powershell -File tools\deploy-site.ps1                # publish website only
#   powershell -File tools\deploy-site.ps1 -WithNginx     # also update nginx config
#
# Switches:
#   -WithNginx   also install server/nginx-salaryticker.conf on the server.
#                The config is validated with 'nginx -t' and rolled back
#                automatically if validation fails (needs sudo for nginx -t /
#                systemctl reload - you may be asked for the server password).
#   -NoVerify    skip the post-upload check
#
# This script is independent from deploy.ps1 (which publishes the app):
#   deploy.ps1       -> exe + version.json + download symlink
#   deploy-site.ps1  -> index.html + assets/ (+ nginx config with -WithNginx)
#
# Server layout assumed by this script:
#   /var/www/salaryticker/                  website root   (deploy.config.json: remoteDir)
#   /etc/nginx/sites-available/salaryticker.conf   nginx config (user-owned)
#   /etc/nginx/sites-enabled/salaryticker.conf     symlink to the above
#
# IMPORTANT: this file must stay ASCII-only (see deploy.ps1 for the reason).
# ============================================================================

param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot "deploy.config.json"),
    [switch]$NoVerify,
    [switch]$WithNginx
)

$ErrorActionPreference = "Stop"

$root    = Split-Path -Parent $PSScriptRoot
$siteDir = Join-Path $root "server"

# nginx config location on the server (matches the deployed layout)
$remoteConf = "/etc/nginx/sites-available/salaryticker.conf"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " SalaryTicker - Website Release" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# ---------------------------------------------------------------- 0. tools
foreach ($tool in @("ssh", "scp")) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        $openssh = Join-Path $env:SystemRoot "System32\OpenSSH\$tool.exe"
        if (-not (Test-Path $openssh)) {
            throw "$tool not found. Enable 'OpenSSH Client' in Windows optional features."
        }
    }
}

# ---------------------------------------------------------------- 1. local files
$indexPath = Join-Path $siteDir "index.html"
$assetsDir = Join-Path $siteDir "assets"
$confPath  = Join-Path $siteDir "nginx-salaryticker.conf"

if (-not (Test-Path $indexPath)) { throw "Not found: $indexPath" }
if (-not (Test-Path $assetsDir)) { throw "Not found: $assetsDir" }
if ($WithNginx -and -not (Test-Path $confPath)) { throw "Not found: $confPath" }

$indexSize = [math]::Round((Get-Item $indexPath).Length / 1KB, 1)
$assetCount = (Get-ChildItem $assetsDir -File).Count
Write-Host "[1/4] Local files OK" -ForegroundColor Yellow
Write-Host "      index.html : $indexSize KB"
Write-Host "      assets/    : $assetCount file(s)"
if ($WithNginx) { Write-Host "      nginx conf : will be installed to $remoteConf" }

# ---------------------------------------------------------------- 2. config
if (-not (Test-Path $ConfigPath)) {
    throw "deploy.config.json not found. Copy deploy.config.sample.json and fill it in."
}
$cfg = Get-Content $ConfigPath -Raw | ConvertFrom-Json

$sshArgs = @("-P", "$($cfg.port)")
$scpArgs = @("-P", "$($cfg.port)")
if ($cfg.keyPath) {
    if (Test-Path $cfg.keyPath) {
        $sshArgs += @("-i", $cfg.keyPath)
        $scpArgs += @("-i", $cfg.keyPath)
    } else {
        Write-Host "      note: keyPath not found ('$($cfg.keyPath)'), using default ssh key" -ForegroundColor DarkYellow
    }
}

$target = "$($cfg.user)@$($cfg.host)"
$remoteDir = $cfg.remoteDir.TrimEnd('/')

# ---------------------------------------------------------------- 3. upload
Write-Host "[2/4] Uploading website to $target ..." -ForegroundColor Yellow

& scp @scpArgs $indexPath "${target}:$remoteDir/index.html"
if ($LASTEXITCODE -ne 0) { throw "scp failed (index.html)" }

# scp -r copies the directory itself into the target path -> <remoteDir>/assets
& scp @scpArgs -r $assetsDir "${target}:$remoteDir/"
if ($LASTEXITCODE -ne 0) { throw "scp failed (assets)" }

# ---- nginx config: stage, validate, roll back on failure -------------------
if ($WithNginx) {
    Write-Host "[3/4] Installing nginx config..." -ForegroundColor Yellow

    # 1) push to a staging path in the home directory
    & scp @scpArgs $confPath "${target}:~/salaryticker.conf.staged"
    if ($LASTEXITCODE -ne 0) { throw "scp failed (nginx config)" }

    # 2) push an installer script (avoids fragile one-liner quoting over ssh)
    #
    # Notes:
    #   - the backup lives right next to the config in sites-available; nginx
    #     never loads it (only sites-enabled/* is included), so it is harmless
    #   - /etc/nginx/sites-available is root-owned, so creating that backup file
    #     needs sudo. sudo caches its timestamp for ~15 minutes, so the password
    #     is asked once and covers the backup, the check and the reload.
    $installer = @'
#!/bin/bash
set -e
CONF=/etc/nginx/sites-available/salaryticker.conf
BACKUP=/etc/nginx/sites-available/salaryticker.conf.bak

sudo cp "$CONF" "$BACKUP"
sudo cp ~/salaryticker.conf.staged "$CONF"

if sudo nginx -t; then
  sudo systemctl reload nginx
  echo "NGINX_OK"
else
  sudo cp "$BACKUP" "$CONF"
  echo "NGINX_ROLLED_BACK"
  exit 1
fi
'@
    $installerPath = Join-Path $env:TEMP "salaryticker-nginx-install.sh"
    [System.IO.File]::WriteAllText($installerPath, ($installer -replace "`r`n", "`n"), (New-Object System.Text.UTF8Encoding($false)))

    & scp @scpArgs $installerPath "${target}:~/salaryticker-nginx-install.sh"
    if ($LASTEXITCODE -ne 0) { throw "scp failed (installer script)" }

    # 3) run it with a TTY so sudo can prompt for the password if needed
    & ssh -t @sshArgs $target "bash ~/salaryticker-nginx-install.sh"
    if ($LASTEXITCODE -ne 0) {
        throw "nginx config rejected by 'nginx -t' - the previous config was restored automatically."
    }
    Write-Host "      nginx config OK (validated and reloaded)" -ForegroundColor Green
}
else {
    Write-Host "[3/4] nginx config not touched (use -WithNginx to update it)" -ForegroundColor DarkYellow
}

# ---------------------------------------------------------------- 4. verify
if ($NoVerify -or -not $cfg.publicUrl) {
    Write-Host "[4/4] Verification skipped" -ForegroundColor DarkYellow
    Write-Host ""
    Write-Host "Website published: $($cfg.publicUrl)/" -ForegroundColor Cyan
    exit 0
}

Write-Host "[4/4] Verifying $($cfg.publicUrl) ..." -ForegroundColor Yellow

# Probe a URL and classify the outcome instead of throwing raw errors.
function Invoke-Probe {
    param([string]$Url)
    $r = [ordered]@{ Code = 0; Body = ""; Server = ""; Error = $null }
    try {
        $resp = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 20
        $r.Code = $resp.StatusCode
        $r.Body = $resp.Content
        try { $r.Server = [string]$resp.Headers['Server'] } catch { }
    }
    catch {
        $ex = $_.Exception
        if ($ex.Response -and $ex.Response.StatusCode) {
            $r.Code = [int]$ex.Response.StatusCode
            try { $r.Server = [string]$ex.Response.Headers['Server'] } catch { }
            try {
                $stream = $ex.Response.GetResponseStream()
                if ($stream) {
                    $reader = New-Object System.IO.StreamReader($stream)
                    $r.Body = $reader.ReadToEnd()
                }
            } catch { }
        }
        else {
            $r.Error = $ex.Message
        }
    }
    return $r
}

$https = Invoke-Probe "$($cfg.publicUrl)/"
$http  = Invoke-Probe ("http://" + $cfg.publicUrl.Replace("https://", "").Replace("http://", "") + "/")

# Aliyun's ICP compliance block: identified by the "Beaver" server header
# (reliable) or the block page body (may be empty in some cases).
$icpBlocked = ($http.Server -match "Beaver") -or
              ($http.Body -match "ICP Filing") -or
              ($http.Body -match "aliyun\.com/beian")

if ($https.Code -eq 200 -and $https.Body -match "SalaryTicker") {
    Write-Host "      home page    : OK (HTTP 200)" -ForegroundColor Green

    $ver = Invoke-Probe "$($cfg.publicUrl)/version.json"
    if ($ver.Code -eq 200 -and $ver.Body -match '"version"') {
        Write-Host "      version.json : OK" -ForegroundColor Green
    } else {
        Write-Host "      version.json : FAILED (HTTP $($ver.Code))" -ForegroundColor Red
        Write-Host "      -> the app's update check depends on this file" -ForegroundColor DarkYellow
    }

    $icon = Invoke-Probe "$($cfg.publicUrl)/assets/app-icon.png"
    if ($icon.Code -eq 200) {
        Write-Host "      assets/      : OK" -ForegroundColor Green
    } else {
        Write-Host "      assets/      : FAILED (HTTP $($icon.Code))" -ForegroundColor Red
    }

    Write-Host ""
    Write-Host "Website published: $($cfg.publicUrl)/" -ForegroundColor Cyan
    exit 0
}

# ---- diagnosis -------------------------------------------------------------
Write-Host "      verification FAILED - diagnosing..." -ForegroundColor Red
Write-Host ""

if ($icpBlocked) {
    Write-Host "  >> The domain is being blocked by Aliyun: ICP filing not compliant." -ForegroundColor Red
    Write-Host "     (HTTP returned Aliyun's 'Non-compliance ICP Filing' page)" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "     This is NOT a server or nginx problem - the upload worked," -ForegroundColor Yellow
    Write-Host "     but Aliyun refuses to serve the domain to visitors." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "     Fix: complete ICP filing at Aliyun (free, 1-3 weeks), or move" -ForegroundColor Cyan
    Write-Host "          to an overseas/HK node which does not require filing." -ForegroundColor Cyan
}
elseif ($https.Error) {
    Write-Host "  >> HTTPS is not reachable from here:" -ForegroundColor Red
    Write-Host "     $($https.Error)" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "     Check: security group allowing 443, certificate file paths," -ForegroundColor Yellow
    Write-Host "            and that nginx is running (systemctl status nginx)." -ForegroundColor Yellow
}
elseif ($https.Code -eq 200) {
    Write-Host "  >> The server answered but the page is not our site." -ForegroundColor Red
    Write-Host "     Check that 'root' in the nginx config points to $remoteDir" -ForegroundColor Yellow
}
else {
    Write-Host "  >> Unexpected response: HTTP $($https.Code)" -ForegroundColor Red
}

exit 1
