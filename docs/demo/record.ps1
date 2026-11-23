<#
.SYNOPSIS
  Records the demo videos (VHS) on Windows. Run from anywhere:
    powershell -ExecutionPolicy Bypass -File docs\demo\record.ps1               (all tapes)
    powershell -ExecutionPolicy Bypass -File docs\demo\record.ps1 01-super-admin (one tape)
  Needs VHS, ttyd and ffmpeg (portable, in C:\SecureShiftTools) and Microsoft Edge (used as the headless browser).
#>
param([string]$Only = "", [string]$ToolsDir = "C:\SecureShiftTools")
$Tapes = @($Only -split "," | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" })

$ErrorActionPreference = "Continue"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$vhs = Get-ChildItem (Join-Path $ToolsDir "vhs") -Recurse -Filter vhs.exe | Select-Object -First 1
$ffmpeg = Get-ChildItem (Join-Path $ToolsDir "ffmpeg") -Recurse -Filter ffmpeg.exe | Select-Object -First 1
if (-not $vhs -or -not $ffmpeg -or -not (Test-Path (Join-Path $ToolsDir "ttyd\ttyd.exe"))) {
    Write-Host "[ERROR] VHS, ttyd or ffmpeg is missing in $ToolsDir." -ForegroundColor Red; exit 1
}
$env:PATH = "$($vhs.DirectoryName);$(Join-Path $ToolsDir 'ttyd');$($ffmpeg.DirectoryName);$env:PATH"
$edge = "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
if (Test-Path $edge) { $env:rod = "bin=$edge" }   # use Edge instead of downloading Chromium

Set-Location $repo
$all = Get-ChildItem (Join-Path $PSScriptRoot "*.tape") | Sort-Object Name
if ($Tapes.Count -gt 0) { $all = $all | Where-Object { $Tapes -contains $_.BaseName } }
foreach ($t in $all) {
    Write-Host "== $($t.Name)" -ForegroundColor Cyan
    $start = Get-Date
    & $vhs.FullName ("docs/demo/" + $t.Name)
    if ($LASTEXITCODE -ne 0) { Write-Host "[ERROR] $($t.Name) failed" -ForegroundColor Red; exit 1 }
    Write-Host ("[OK] {0} in {1:0} s" -f $t.Name, ((Get-Date) - $start).TotalSeconds) -ForegroundColor Green
}
