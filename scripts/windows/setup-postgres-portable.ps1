<#
.SYNOPSIS
  Portable PostgreSQL 16 for SecureShift on a laptop WITHOUT admin rights and WITHOUT Docker.

.DESCRIPTION
  1. Uses a PostgreSQL 16 zip that is already here (next to this script, in the current folder or in
     C:\SecureShiftTools: postgresql-16*-windows-x64-binaries.zip) - NO internet needed - or downloads it
  2. Unzips it to C:\SecureShiftTools\pgsql   (nothing is installed, no Windows service, no admin)
  3. Creates the data folder C:\SecureShiftTools\pgdata (initdb) the first time
  4. Starts the server on port 5434, only on this computer (localhost), with pg_ctl
  5. Creates the database "secureshift" and the empty schema "dev"
  6. Prints the connection string (and saves it in C:\SecureShiftTools\connection-string.txt)
  Running it again is safe: it skips what is already done and just (re)starts the server.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\windows\setup-postgres-portable.ps1

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\windows\setup-postgres-portable.ps1 -ZipPath D:\Downloads\postgresql-16.11-1-windows-x64-binaries.zip
#>
param(
    [string]$ToolsDir = "C:\SecureShiftTools",
    [int]$Port = 5434,
    [string]$Version = "16.11-1",
    [string]$ZipPath = "",
    [string]$Database = "secureshift"
)

# Native programs (pg_ctl, psql, dotnet) print to stderr; with "Stop" PowerShell 5.1 would treat that as a crash.
# Every step checks $LASTEXITCODE instead.
$ErrorActionPreference = "Continue"
$ProgressPreference = "SilentlyContinue"   # makes Invoke-WebRequest much faster

function Step($text) { Write-Host ""; Write-Host "== $text" -ForegroundColor Cyan }
function Ok($text) { Write-Host "[OK] $text" -ForegroundColor Green }
function Fail($text) { Write-Host "[ERROR] $text" -ForegroundColor Red; exit 1 }

$pgDir = Join-Path $ToolsDir "pgsql"
$dataDir = Join-Path $ToolsDir "pgdata"
$logFile = Join-Path $ToolsDir "postgres.log"
$pwFile = Join-Path $ToolsDir "postgres-password.txt"
$connFile = Join-Path $ToolsDir "connection-string.txt"
$bin = Join-Path $pgDir "bin"

New-Item -ItemType Directory -Force $ToolsDir | Out-Null

# ---------------------------------------------------------------- 1 + 2. binaries
Step "1. PostgreSQL 16 binaries in $pgDir"
if (Test-Path (Join-Path $bin "pg_ctl.exe")) {
    Ok "Already unzipped."
} else {
    # No internet needed when a PostgreSQL 16 zip is already here: next to this script (offline package),
    # in the current folder, or in C:\SecureShiftTools. Only when none is found, it is downloaded.
    if ($ZipPath -eq "") {
        foreach ($dir in @($PSScriptRoot, (Get-Location).Path, $ToolsDir) | Select-Object -Unique) {
            $found = Get-ChildItem -Path $dir -Filter "postgresql-16*-windows-x64-binaries.zip" -File -ErrorAction SilentlyContinue |
                Sort-Object Name -Descending | Select-Object -First 1
            if ($found) { $ZipPath = $found.FullName; break }
        }
    }
    if ($ZipPath -ne "" -and -not (Test-Path $ZipPath)) { Fail "The zip $ZipPath does not exist." }
    if ($ZipPath -eq "") {
        $ZipPath = Join-Path $ToolsDir "postgresql-$Version-windows-x64-binaries.zip"
        $url = "https://get.enterprisedb.com/postgresql/postgresql-$Version-windows-x64-binaries.zip"
        Write-Host "No PostgreSQL zip found next to this script or in $ToolsDir, so it is downloaded:"
        Write-Host "$url  (about 310 MB, this can take a few minutes)"
        try { Invoke-WebRequest -Uri $url -OutFile $ZipPath -UseBasicParsing -ErrorAction Stop }
        catch { Fail "Download failed: $($_.Exception.Message). No internet? Copy postgresql-$Version-windows-x64-binaries.zip next to this script (e.g. from a pen drive) and run again." }
    } else {
        Write-Host "Using the zip $ZipPath (no download needed)"
    }
    Write-Host "Unzipping to $ToolsDir (about 1 minute) ..."
    # .NET's ZipFile instead of Expand-Archive: much faster, and no progress bar drawn over the window
    if (Test-Path $pgDir) { Remove-Item -Recurse -Force $pgDir }   # a half-unzipped folder from an earlier try
    try {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::ExtractToDirectory($ZipPath, $ToolsDir)
    }
    catch { Fail "Could not unzip $($ZipPath): $($_.Exception.Message)" }
    if (-not (Test-Path (Join-Path $bin "pg_ctl.exe"))) { Fail "The zip did not contain pgsql\bin\pg_ctl.exe. Is it the 'binaries' zip from EnterpriseDB?" }
    Ok "Unzipped."
}
& (Join-Path $bin "postgres.exe") --version

# ---------------------------------------------------------------- 3. data folder
Step "2. Data folder $dataDir"
if (Test-Path (Join-Path $dataDir "PG_VERSION")) {
    Ok "Already created."
} else {
    # a random password for the local "postgres" user, saved outside the repository
    $chars = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789"
    $password = -join (1..20 | ForEach-Object { $chars[(Get-Random -Maximum $chars.Length)] })
    Set-Content -Path $pwFile -Value $password -NoNewline -Encoding ascii
    & (Join-Path $bin "initdb.exe") -D $dataDir -U postgres -A scram-sha-256 --pwfile=$pwFile -E UTF8 --no-locale | Out-Host
    if ($LASTEXITCODE -ne 0) { Fail "initdb failed (see the messages above)." }
    Ok "Created. The password of the local 'postgres' user is in $pwFile"
}
if (-not (Test-Path $pwFile)) { Fail "$pwFile is missing. Delete $dataDir and run this script again (this deletes the local database)." }
$password = (Get-Content $pwFile -Raw).Trim()
$env:PGPASSWORD = $password

# ---------------------------------------------------------------- 4. start
Step "3. Start the server on port $Port (localhost only)"
& (Join-Path $bin "pg_ctl.exe") -D $dataDir status *> $null
if ($LASTEXITCODE -eq 0) {
    Ok "Already running."
} else {
    $listener = $null
    try {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
        $listener.Start()
    } catch {
        Fail "Port $Port is already used by another program. Run again with another port, e.g. -Port 5435 (and use it in the connection string)."
    } finally {
        if ($listener) { $listener.Stop() }
    }
    # Started in its own hidden window: the server keeps running after this script (and this window) ends,
    # and it never holds this window's output open. -w = wait until the server accepts connections.
    $p = Start-Process -FilePath (Join-Path $bin "pg_ctl.exe") -WindowStyle Hidden -PassThru `
        -ArgumentList @("-D", "`"$dataDir`"", "-l", "`"$logFile`"", "-o", "`"-p $Port -c listen_addresses=localhost`"", "-w", "start")
    $p.WaitForExit()
    if ($p.ExitCode -ne 0) { Fail "The server did not start. Look at $logFile" }
    Ok "Started (log: $logFile)."
}

# ---------------------------------------------------------------- 5. database + schema
Step "4. Database '$Database' and schema 'dev'"
$psql = Join-Path $bin "psql.exe"
$exists = & $psql -h 127.0.0.1 -p $Port -U postgres -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '$Database'"
if ($LASTEXITCODE -ne 0) { Fail "Could not connect to the server (wrong port or password?). Look at $logFile" }
if ($exists -ne "1") {
    & $psql -h 127.0.0.1 -p $Port -U postgres -d postgres -v ON_ERROR_STOP=1 -qc "CREATE DATABASE $Database"
    Ok "Database $Database created."
} else {
    Ok "Database $Database already exists."
}
& $psql -h 127.0.0.1 -p $Port -U postgres -d $Database -v ON_ERROR_STOP=1 -qc "CREATE SCHEMA IF NOT EXISTS dev"
Ok "Schema dev is there."

# ---------------------------------------------------------------- 6. connection string
$conn = "Host=127.0.0.1;Port=$Port;Database=$Database;Username=postgres;Password=$password"
Set-Content -Path $connFile -Value $conn -NoNewline -Encoding ascii
Step "Done. Connection string (saved in $connFile; the password is not shown here):"
Write-Host ($conn -replace 'Password=.*', 'Password=********')
Write-Host ""
Write-Host "Use it in this PowerShell window:"
Write-Host "  `$env:ConnectionStrings__SecureShiftDb = Get-Content $connFile"
Write-Host "Or simply run:  powershell -ExecutionPolicy Bypass -File `"$(Join-Path $PSScriptRoot 'run.ps1')`""
