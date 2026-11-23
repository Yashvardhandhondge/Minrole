<#
.SYNOPSIS
  Stops the portable PostgreSQL started by setup-postgres-portable.ps1 (the data is kept).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\windows\stop-postgres.ps1
#>
param([string]$ToolsDir = "C:\SecureShiftTools")

$pgCtl = Join-Path $ToolsDir "pgsql\bin\pg_ctl.exe"
$dataDir = Join-Path $ToolsDir "pgdata"
if (-not (Test-Path $pgCtl)) { Write-Host "[INFO] No portable PostgreSQL in $ToolsDir. Nothing to stop."; exit 0 }

& $pgCtl -D $dataDir status *> $null
if ($LASTEXITCODE -ne 0) { Write-Host "[INFO] PostgreSQL is not running."; exit 0 }

& $pgCtl -D $dataDir stop -m fast | Out-Host
if ($LASTEXITCODE -eq 0) { Write-Host "[OK] PostgreSQL stopped. Your data is kept in $dataDir." -ForegroundColor Green }
else { Write-Host "[ERROR] Could not stop PostgreSQL (see above)." -ForegroundColor Red; exit 1 }
