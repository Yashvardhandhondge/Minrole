<#
.SYNOPSIS
  Starts SecureShift on this laptop: portable PostgreSQL + the console app.

.DESCRIPTION
  1. Starts the portable PostgreSQL (setup-postgres-portable.ps1 does the first-time setup)
  2. Sets ConnectionStrings__SecureShiftDb for THIS PowerShell window only
  3. Builds the app (Release) the first time, or with -Build
     (in the OFFLINE package the app is already published in app\, so nothing is built)
  4. First run on the empty database: "db setup --yes" (28 tables, 4 views, 569 sample rows)
  5. Starts the app (login screen)

  -Reset   deletes the local database "secureshift" and creates it again with the sample data
           (only the LOCAL portable database; it never touches the faculty database)

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\windows\run.ps1

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\windows\run.ps1 -Reset
#>
param(
    [string]$ToolsDir = "C:\SecureShiftTools",
    [int]$Port = 5434,
    [switch]$Reset,
    [switch]$Build,
    [switch]$SetupOnly
)

# Native programs (pg_ctl, psql, dotnet) print to stderr; with "Stop" PowerShell 5.1 would treat that as a crash.
# Every step checks $LASTEXITCODE instead.
$ErrorActionPreference = "Continue"
$connFile = Join-Path $ToolsDir "connection-string.txt"

# Two layouts: the OFFLINE package (run.ps1 next to app\SecureShift.Console.dll, already published - no build,
# no Git, no internet) or the Git repository (scripts\windows\run.ps1, the app is built the first time).
$packagedApp = Join-Path $PSScriptRoot "app\SecureShift.Console.dll"
$packaged = Test-Path $packagedApp
if ($packaged) {
    $repo = $PSScriptRoot
    $app = $packagedApp
} else {
    $repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
    $app = Join-Path $repo "src\SecureShift.Console\bin\Release\net8.0\SecureShift.Console.dll"
}

function Step($text) { Write-Host ""; Write-Host "== $text" -ForegroundColor Cyan }

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "[ERROR] 'dotnet' was not found. Install the .NET 8 runtime or SDK first (see TCS_LAPTOP_SETUP.md)." -ForegroundColor Red
    exit 1
}

Step "PostgreSQL"
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "setup-postgres-portable.ps1") -ToolsDir $ToolsDir -Port $Port | Out-Host
if ($LASTEXITCODE -ne 0) { exit 1 }

$env:ConnectionStrings__SecureShiftDb = (Get-Content $connFile -Raw).Trim()
Write-Host "[OK] ConnectionStrings__SecureShiftDb is set for this window." -ForegroundColor Green

if ($Reset) {
    Step "Reset: delete and recreate the LOCAL database 'secureshift'"
    $env:PGPASSWORD = ((Get-Content $connFile -Raw) -replace '.*Password=', '').Trim()
    $psql = Join-Path $ToolsDir "pgsql\bin\psql.exe"
    & $psql -h 127.0.0.1 -p $Port -U postgres -d postgres -v ON_ERROR_STOP=1 -qc "DROP DATABASE IF EXISTS secureshift WITH (FORCE)" -c "CREATE DATABASE secureshift"
    & $psql -h 127.0.0.1 -p $Port -U postgres -d secureshift -v ON_ERROR_STOP=1 -qc "CREATE SCHEMA dev"
    Write-Host "[OK] Empty database secureshift with schema dev." -ForegroundColor Green
}

if ($packaged) {
    Write-Host "[OK] Offline package: using the published app $app (nothing to build)." -ForegroundColor Green
} elseif ($Build -or -not (Test-Path $app)) {
    Step "Build (Release)"
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
    $env:DOTNET_NOLOGO = "1"
    & dotnet build (Join-Path $repo "SecureShift.sln") -c Release -m:1 -v q -nologo
    if ($LASTEXITCODE -ne 0) { Write-Host "[ERROR] Build failed (see above)." -ForegroundColor Red; exit 1 }
}

Step "Database tables"
Push-Location $repo
try {
    & dotnet $app db status *> $null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "The database is empty: creating the 28 tables, 4 views and the sample data (db setup)."
        & dotnet $app db setup --yes
        if ($LASTEXITCODE -ne 0) { exit 1 }
    } else {
        Write-Host "[OK] All SecureShift tables are there." -ForegroundColor Green
    }
    if ($SetupOnly) { exit 0 }

    Step "SecureShift (type 0 on the login screen to exit)"
    & dotnet $app
} finally {
    Pop-Location
}
