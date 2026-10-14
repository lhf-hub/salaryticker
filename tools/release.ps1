# ============================================================================
# release.ps1 - One-click Git release for SalaryTicker
#               test + build + commit + tag + push + GitHub Release
#
# Usage:
#   1) Copy github.config.sample.json to github.config.json and fill in the token
#   2) Edit <Version> in SalaryTicker.csproj and tools\release-notes.txt
#   3) powershell -File tools\release.ps1
#
#   -Message "<msg>"  Commit message (Conventional Commits, Chinese is fine).
#                     When omitted you are prompted; press Enter to accept the
#                     default "chore(release): v<version>".
#   -SkipBuild        Reuse the exe already in dist\ instead of re-publishing.
#   -NoTag            Everyday push: test + commit + push WITHOUT cutting a tag
#                     or creating a Release (and without building). Use this
#                     while the csproj version has not been bumped yet -
#                     otherwise the "tag already exists" guard blocks the push
#                     until the version changes, even for a one-line docs fix.
#   -SkipPush         Test + build + commit + tag locally only (dry run, nothing
#                     leaves this machine).
#
# Scope: the CODE - this git repository only.
#   Deploying the APP to the server  -> tools\deploy.ps1
#   Deploying the WEBSITE            -> tools\deploy-site.ps1
#
# The GitHub Release is an ARCHIVE, not a distribution channel: the app's
# auto-updater still points at our own server and never looks at GitHub. The
# exe is attached anyway so the two channels hand out the same binary - hence
# the build here, reused by "deploy.ps1 -SkipPublish" afterwards.
#
# IMPORTANT: this file must stay ASCII-only.
#   Windows PowerShell 5.1 decodes .ps1 files without a BOM using the system
#   ANSI codepage (GBK on Chinese Windows). Non-ASCII characters here would be
#   mis-decoded and can corrupt string literals, breaking the script.
#   Data files it READS are still handled as UTF-8 explicitly.
# ============================================================================

param(
    [string]$Message = "",
    [string]$ConfigPath = (Join-Path $PSScriptRoot "github.config.json"),
    [switch]$SkipPush,
    [switch]$NoTag,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$root      = Split-Path -Parent $PSScriptRoot
$csproj    = Join-Path $root "SalaryTicker.csproj"
$notesFile = Join-Path $PSScriptRoot "release-notes.txt"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " SalaryTicker - Git Release" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# ---------------------------------------------------------------- helpers
function Invoke-Git {
    # Runs git with the configured proxy and returns its stdout as a string array.
    #
    # $ErrorActionPreference is relaxed around the native call on purpose: in
    # PowerShell 5.1 ANY stderr line from a native command is turned into an
    # ErrorRecord, and with the script-wide "Stop" in effect a harmless git
    # warning would abort the release. Instead stderr goes straight to the
    # console and $LASTEXITCODE alone decides success.
    # stdout is deliberately NOT merged with stderr, so callers can trust it.
    param([string[]]$GitArgs)
    $all = @()
    if ($script:gitProxyArgs) { $all += $script:gitProxyArgs }
    $all += $GitArgs

    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $out = & git @all
        $code = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $prevEap
    }
    if ($code -ne 0) {
        throw "git $($GitArgs -join ' ') failed (exit $code)"
    }
    return @($out)
}

function Invoke-CurlJson {
    # POSTs a file body to the GitHub API with curl.exe and returns the parsed
    # JSON. curl.exe (not Invoke-RestMethod) because it streams large files
    # without loading them into memory, and because the asset upload has to go
    # through the same proxy as git.
    param([string]$Url, [string]$BodyFile, [string]$ContentType)

    $respFile = [System.IO.Path]::GetTempFileName()
    $curlArgs = @("-sS", "-X", "POST",
                  "-H", "Authorization: Bearer $($cfg.token)",
                  "-H", "Accept: application/vnd.github+json",
                  "-H", "X-GitHub-Api-Version: 2022-11-28",
                  "-H", "User-Agent: salaryticker-release",
                  "-H", "Content-Type: $ContentType",
                  "--data-binary", "@$BodyFile",
                  "--connect-timeout", "30",
                  "--max-time", "1800",
                  "-o", $respFile,
                  "-w", "%{http_code}")
    if ($cfg.proxy) { $curlArgs += @("-x", $cfg.proxy) }
    $curlArgs += $Url

    $status = & curl.exe @curlArgs
    $code = "$status".Trim()
    $respText = if (Test-Path $respFile) { Get-Content $respFile -Raw -Encoding UTF8 } else { "" }
    Remove-Item $respFile -Force -ErrorAction SilentlyContinue

    if ($code -notmatch '^2\d\d$') {
        throw "GitHub API returned HTTP $code for $Url`n$respText"
    }
    if (-not $respText) { return $null }
    return $respText | ConvertFrom-Json
}

# ---------------------------------------------------------------- 0. config
if (-not (Test-Path $ConfigPath)) {
    throw "Config not found: $ConfigPath`nCopy github.config.sample.json to github.config.json and fill in your token."
}
$cfg = Get-Content $ConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($key in @("owner", "repo", "token")) {
    if (-not $cfg.$key) { throw "github.config.json is missing '$key'" }
}
if ($cfg.token -like "*REPLACE_WITH_YOUR_TOKEN*") {
    throw "github.config.json still holds the placeholder token."
}
$slug = "$($cfg.owner)/$($cfg.repo)"

$script:gitProxyArgs = @()
if ($cfg.proxy) {
    $script:gitProxyArgs = @("-c", "http.proxy=$($cfg.proxy)", "-c", "https.proxy=$($cfg.proxy)")
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw "git not found on PATH" }
if (-not (Get-Command curl.exe -ErrorAction SilentlyContinue)) {
    throw "curl.exe not found. It ships with Windows 10 1803+ at C:\Windows\System32\curl.exe"
}

# Locate dotnet even when it is not on PATH (common when launched from a shell
# that does not inherit the Windows PATH).
$dotnet = "dotnet"
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $candidate = Join-Path $env:ProgramFiles "dotnet\dotnet.exe"
    if (Test-Path $candidate) { $dotnet = $candidate }
    else { throw "dotnet SDK not found. Please install the .NET 8 SDK first." }
}

# ---------------------------------------------------------------- 1. version
# Read as UTF-8 explicitly: the csproj contains Chinese comments, and
# Get-Content without -Encoding would decode them with the ANSI codepage.
$csprojText = Get-Content $csproj -Raw -Encoding UTF8
if ($csprojText -notmatch '<Version>([^<]+)</Version>') {
    throw "Cannot read <Version> from SalaryTicker.csproj"
}
$version = $Matches[1].Trim()
$tag     = "v$version"
Write-Host "[1/7] Version: $version   Tag: $tag" -ForegroundColor Yellow

# ---------------------------------------------------------------- 2. guards
# A tag must never move. If it exists the release already happened - bump the
# version in the csproj instead of deleting the tag.
# Where-Object { $_ } drops empty lines so an empty result really means "none".
if (-not $NoTag) {
    $localTag = @(Invoke-Git @("tag", "-l", $tag)) | Where-Object { $_ }
    if ($localTag) { throw "Tag $tag already exists locally. Bump <Version> in SalaryTicker.csproj, or use -NoTag to push without cutting a release." }
}

$branch = @(Invoke-Git @("rev-parse", "--abbrev-ref", "HEAD")) | Where-Object { $_ } | Select-Object -First 1
if ($branch -ne "main") { throw "Releases are cut from 'main' only (currently on '$branch')." }

$dirty = @(Invoke-Git @("status", "--porcelain")) | Where-Object { $_ }
$dirtyNote = if ($dirty) { "$($dirty.Count) pending change(s)" } else { "working tree clean" }
Write-Host "[2/7] Guards OK (branch main, tag free, $dirtyNote)" -ForegroundColor Yellow

# ---------------------------------------------------------------- 3. test
# The gate that must pass before a tag is cut: dotnet test also builds the app,
# so a green run means "compiles + all cases pass".
Write-Host "[3/7] dotnet test ..." -ForegroundColor Yellow
Push-Location $root
$prevEap = $ErrorActionPreference
$ErrorActionPreference = "Continue"
try {
    & $dotnet test --nologo
    $testCode = $LASTEXITCODE
} finally {
    $ErrorActionPreference = $prevEap
    Pop-Location
}
if ($testCode -ne 0) { throw "dotnet test failed (exit $testCode) - not tagging a red build." }

# ---------------------------------------------------------------- 4. build
# The Release asset is the SAME exe the auto-updater serves, so the two channels
# hand out one binary. Build delegates to deploy.ps1 -SkipUpload, which owns the
# publish flags; the later "deploy.ps1 -SkipPublish" uploads this very file
# without rebuilding.
$assetName = "SalaryTicker-$version.exe"
$assetPath = Join-Path $root "dist\upload\$assetName"

if ($NoTag) {
    Write-Host "[4/7] -NoTag: no Release, skipping the build" -ForegroundColor DarkYellow
} elseif ($SkipBuild) {
    Write-Host "[4/7] -SkipBuild: reusing the existing build" -ForegroundColor DarkYellow
} else {
    Write-Host "[4/7] Building via deploy.ps1 -SkipUpload (1-3 min)..." -ForegroundColor Yellow
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "deploy.ps1") -SkipUpload
    if ($LASTEXITCODE -ne 0) { throw "deploy.ps1 -SkipUpload failed (exit $LASTEXITCODE)" }
}
if (-not $NoTag -and -not (Test-Path $assetPath)) {
    throw "Build artifact not found: $assetPath`nRun 'powershell -File tools\deploy.ps1 -SkipUpload' first, or drop -SkipBuild."
}

# ---------------------------------------------------------------- 4. commit
if ($dirty) {
    if (-not $Message) {
        Write-Host ""
        Write-Host "Enter the commit message (Conventional Commits)." -ForegroundColor Yellow
        Write-Host "Press Enter alone to use: chore(release): $tag" -ForegroundColor DarkGray
        $entered = Read-Host "Message"
        $Message = if ($entered) { $entered } else { "chore(release): $tag" }
    }
    Invoke-Git @("add", "-A") | Out-Null
    Invoke-Git @("commit", "-m", $Message) | Out-Null
    Write-Host "[5/7] Committed: $Message" -ForegroundColor Yellow
} else {
    $action = if ($NoTag) { "pushing" } else { "tagging" }
    Write-Host "[5/7] Nothing to commit - $action the current HEAD" -ForegroundColor DarkYellow
}

# ---------------------------------------------------------------- 5. tag
# Annotated tag: carries author and date, and 'git describe' can use it.
if ($NoTag) {
    Write-Host "[6/7] -NoTag: no version tag (everyday push)" -ForegroundColor DarkYellow
} else {
    Invoke-Git @("tag", "-a", $tag, "-m", "SalaryTicker $tag") | Out-Null
    Write-Host "[6/7] Tagged $tag" -ForegroundColor Yellow
}

if ($SkipPush) {
    Write-Host ""
    Write-Host "[7/7] -SkipPush: nothing pushed." -ForegroundColor DarkYellow
    if ($NoTag) {
        Write-Host "      Local only. Undo with: git reset --soft HEAD~1" -ForegroundColor DarkYellow
    } else {
        Write-Host "      Local only. Undo with: git tag -d $tag  (and git reset --soft HEAD~1)" -ForegroundColor DarkYellow
    }
    exit 0
}

# ---------------------------------------------------------------- 7. push
# The token goes into the push URL for this one command only, so it is never
# written into .git/config and never shows up in 'git remote -v'.
$what = if ($NoTag) { "main" } else { "main + tags" }
Write-Host "[7/7] Pushing $what to $slug ..." -ForegroundColor Yellow
$pushUrl = "https://x-access-token:$($cfg.token)@github.com/$slug.git"
Invoke-Git @("push", $pushUrl, "main", "--follow-tags") | Out-Null

# ---------------------------------------------------------------- 8. release
if ($NoTag) {
    Write-Host "      -NoTag: no GitHub Release" -ForegroundColor DarkYellow
} else {
    # Release notes may contain Chinese - read them as UTF-8.
    $notes = if (Test-Path $notesFile) { (Get-Content $notesFile -Raw -Encoding UTF8).Trim() } else { "" }
    if (-not $notes) { $notes = "SalaryTicker $tag" }

    $bodyFile = [System.IO.Path]::GetTempFileName()
    # ConvertTo-Json escapes non-ASCII, and the file is written as UTF-8 without
    # a BOM, so the Chinese notes survive the round trip to the API.
    $body = [ordered]@{
        tag_name         = $tag
        target_commitish = "main"
        name             = $tag
        body             = $notes
        draft            = $false
        prerelease       = $false
    } | ConvertTo-Json -Depth 4
    [System.IO.File]::WriteAllText($bodyFile, $body, (New-Object System.Text.UTF8Encoding($false)))

    Write-Host "      Creating GitHub Release $tag ..." -ForegroundColor Yellow
    $release = Invoke-CurlJson -Url "https://api.github.com/repos/$slug/releases" `
                               -BodyFile $bodyFile `
                               -ContentType "application/json; charset=utf-8"
    Remove-Item $bodyFile -Force -ErrorAction SilentlyContinue

    Write-Host "      Uploading $assetName ..." -ForegroundColor Yellow
    $uploadUrl = "https://uploads.github.com/repos/$slug/releases/$($release.id)/assets?name=$assetName"
    Invoke-CurlJson -Url $uploadUrl -BodyFile $assetPath -ContentType "application/octet-stream" | Out-Null
    Write-Host "      $($release.html_url)" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host ""
Write-Host "Version : $version"
Write-Host "Commit  : $(@(Invoke-Git @('rev-parse', '--short', 'HEAD')) | Where-Object { $_ } | Select-Object -First 1)"
if ($NoTag) {
    Write-Host "Tag     : (none - everyday push)"
    Write-Host ""
    Write-Host "No tag was cut, so nothing new to ship. Use this script without -NoTag at release time." -ForegroundColor Gray
} else {
    Write-Host "Tag     : $tag"
    Write-Host ""
    Write-Host "Next: powershell -File tools\deploy.ps1 -SkipPublish   (serve this same exe to the auto-updater)" -ForegroundColor Gray
}
