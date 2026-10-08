# ============================================================================
# release.ps1 - One-click Git release for SalaryTicker
#               test + commit + tag + push
#
# Usage:
#   1) Copy github.config.sample.json to github.config.json and fill in the token
#   2) Edit <Version> in SalaryTicker.csproj and tools\release-notes.txt
#   3) powershell -File tools\release.ps1
#
#   -Message "<msg>"  Commit message (Conventional Commits, Chinese is fine).
#                     When omitted you are prompted; press Enter to accept the
#                     default "chore(release): v<version>".
#   -NoTag            Everyday push: test + commit + push WITHOUT cutting a tag.
#                     Use this while the csproj version has not been bumped yet -
#                     otherwise the "tag already exists" guard blocks the push
#                     until the version changes, even for a one-line docs fix.
#   -SkipPush         Test + commit (+ tag) locally only (dry run, nothing
#                     leaves this machine).
#
# Scope: the CODE - this git repository only.
#   Building and shipping the APP to the server -> tools\deploy.ps1
#   Deploying the WEBSITE                       -> tools\deploy-site.ps1
#
# The repository is PRIVATE and intentionally carries no GitHub Release: this
# script only puts the source history and the version tag on GitHub. The shipped
# exe lives on the server, where deploy.ps1 uploads it for the auto-updater.
# A release commit is therefore never blocked on a build - dotnet test is the
# gate that has to pass before a tag is cut.
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
    [switch]$NoTag
)

$ErrorActionPreference = "Stop"

$root   = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $root "SalaryTicker.csproj"

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
Write-Host "[1/6] Version: $version   Tag: $tag" -ForegroundColor Yellow

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
Write-Host "[2/6] Guards OK (branch main, tag free, $dirtyNote)" -ForegroundColor Yellow

# ---------------------------------------------------------------- 3. test
# The gate that must pass before a tag is cut: dotnet test also builds the app,
# so a green run means "compiles + all cases pass".
Write-Host "[3/6] dotnet test ..." -ForegroundColor Yellow
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
    Write-Host "[4/6] Committed: $Message" -ForegroundColor Yellow
} else {
    $action = if ($NoTag) { "pushing" } else { "tagging" }
    Write-Host "[4/6] Nothing to commit - $action the current HEAD" -ForegroundColor DarkYellow
}

# ---------------------------------------------------------------- 5. tag
# Annotated tag: carries author and date, and 'git describe' can use it.
if ($NoTag) {
    Write-Host "[5/6] -NoTag: no version tag (everyday push)" -ForegroundColor DarkYellow
} else {
    Invoke-Git @("tag", "-a", $tag, "-m", "SalaryTicker $tag") | Out-Null
    Write-Host "[5/6] Tagged $tag" -ForegroundColor Yellow
}

if ($SkipPush) {
    Write-Host ""
    Write-Host "[6/6] -SkipPush: nothing pushed." -ForegroundColor DarkYellow
    if ($NoTag) {
        Write-Host "      Local only. Undo with: git reset --soft HEAD~1" -ForegroundColor DarkYellow
    } else {
        Write-Host "      Local only. Undo with: git tag -d $tag  (and git reset --soft HEAD~1)" -ForegroundColor DarkYellow
    }
    exit 0
}

# ---------------------------------------------------------------- 6. push
# The token goes into the push URL for this one command only, so it is never
# written into .git/config and never shows up in 'git remote -v'.
$what = if ($NoTag) { "main" } else { "main + tags" }
Write-Host "[6/6] Pushing $what to $slug ..." -ForegroundColor Yellow
$pushUrl = "https://x-access-token:$($cfg.token)@github.com/$slug.git"
Invoke-Git @("push", $pushUrl, "main", "--follow-tags") | Out-Null

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
    Write-Host "Next: powershell -File tools\deploy.ps1   (build and ship this version to the auto-updater)" -ForegroundColor Gray
}
