# 一次性脚本：把服务器上已有的版本归档补成 GitHub Release（用完即删，不进版本控制）
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

$cfg  = Get-Content (Join-Path $PSScriptRoot "github.config.json") -Raw -Encoding UTF8 | ConvertFrom-Json
$hist = Get-Content (Join-Path $PSScriptRoot "version-history.json") -Raw -Encoding UTF8 | ConvertFrom-Json
$slug = "$($cfg.owner)/$($cfg.repo)"
$ua   = "salaryticker-backfill"

function ApiPost($Url, $BodyFile, $ContentType) {
    $resp = [System.IO.Path]::GetTempFileName()
    $a = @("-sS","-X","POST",
           "-H","Authorization: Bearer $($cfg.token)",
           "-H","Accept: application/vnd.github+json",
           "-H","X-GitHub-Api-Version: 2022-11-28",
           "-H","User-Agent: $ua",
           "-H","Content-Type: $ContentType",
           "--data-binary","@$BodyFile",
           "--connect-timeout","30","--max-time","1800",
           "-o",$resp,"-w","%{http_code}")
    if ($cfg.proxy) { $a += @("-x", $cfg.proxy) }
    $a += $Url
    $code = (& curl.exe @a).Trim()
    $text = if (Test-Path $resp) { Get-Content $resp -Raw -Encoding UTF8 } else { "" }
    Remove-Item $resp -Force -ErrorAction SilentlyContinue
    if ($code -notmatch '^2\d\d$') { throw "HTTP $code for $Url`n$text" }
    if ($text) { return $text | ConvertFrom-Json }
    return $null
}

# GitHub 把"创建时间最晚"的 Release 标为 Latest，所以从旧到新建，让 v1.1.8 成为 Latest
$versions = @($hist.versions | Where-Object { $_.releaseNotes } | Sort-Object { [version]$_.version })

foreach ($v in $versions) {
    $tag = "v$($v.version)"
    Write-Host ("--- $tag ---")

    $bodyFile = [System.IO.Path]::GetTempFileName()
    $json = [ordered]@{ tag_name = $tag; name = $tag; body = $v.releaseNotes; draft = $false; prerelease = $false } | ConvertTo-Json -Depth 4
    [System.IO.File]::WriteAllText($bodyFile, $json, (New-Object System.Text.UTF8Encoding($false)))
    $rel = ApiPost "https://api.github.com/repos/$slug/releases" $bodyFile "application/json; charset=utf-8"
    Remove-Item $bodyFile -Force
    Write-Host ("    Release 已建 id=" + $rel.id)

    $exe = [System.IO.Path]::GetTempFileName()
    $url = "https://salaryticker.leelab.com.cn/download/releases/$($v.archive)"
    & curl.exe -sS --connect-timeout 30 --max-time 900 -o $exe $url
    $size = (Get-Item $exe).Length
    if ($size -lt 1000000) { throw "下载的 exe 太小（$size 字节），可能不是有效文件：$url" }
    Write-Host ("    已下载 " + [math]::Round($size/1MB,1) + " MB")

    ApiPost "https://uploads.github.com/repos/$slug/releases/$($rel.id)/assets?name=$($v.archive)" $exe "application/octet-stream" | Out-Null
    Remove-Item $exe -Force
    Write-Host "    附件已上传"
}

Write-Host ""
Write-Host "全部完成"
$final = & curl.exe -sS -H "Authorization: Bearer $($cfg.token)" -H "Accept: application/vnd.github+json" -H "User-Agent: $ua" -H "X-GitHub-Api-Version: 2022-11-28" -x $cfg.proxy "https://api.github.com/repos/$slug/releases"
foreach ($r in ($final | ConvertFrom-Json)) { Write-Host ("  " + $r.tag_name + "  附件 " + @($r.assets).Count + " 个") }
