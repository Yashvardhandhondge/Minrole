<#
.SYNOPSIS
  Builds SecureShift-offline.zip: everything needed to run SecureShift on a laptop with NO Git, NO build
  and NO internet (only the .NET 8 runtime or SDK). Run it on a machine that has the .NET 8 SDK:
    powershell -ExecutionPolicy Bypass -File scripts\windows\make-offline-package.ps1

.DESCRIPTION
  The zip contains one folder "SecureShift" with:
    app\                       the published app (dotnet publish, framework-dependent, net8.0)
    database\postgres\         the PostgreSQL scripts
    samples\                   the Governance sample input files
    run.ps1, setup-postgres-portable.ps1, stop-postgres.ps1
    docs\TCS_LAPTOP_SETUP.md, docs\USER_GUIDE.md
    START-HERE.txt
  The PostgreSQL zip (postgresql-16.11-1-windows-x64-binaries.zip, ~310 MB) is NOT inside: put it next to
  run.ps1 after unzipping (from a pen drive), and setup-postgres-portable.ps1 uses it without internet.
#>
param([string]$Output = "")

$ErrorActionPreference = "Continue"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
if ($Output -eq "") { $Output = Join-Path $repo "SecureShift-offline.zip" }
$stage = Join-Path ([IO.Path]::GetTempPath()) ("ss-offline-" + [guid]::NewGuid().ToString("N").Substring(0, 8))
$pkg = Join-Path $stage "SecureShift"
function Fail($text) { Write-Host "[ERROR] $text" -ForegroundColor Red; Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue; exit 1 }

Write-Host "== 1. dotnet publish (Release, framework-dependent net8.0)" -ForegroundColor Cyan
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"; $env:DOTNET_NOLOGO = "1"
& dotnet publish (Join-Path $repo "src\SecureShift.Console\SecureShift.Console.csproj") -c Release -o (Join-Path $pkg "app") --self-contained false -m:1 -v q -nologo
if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $pkg "app\SecureShift.Console.dll"))) { Fail "dotnet publish failed." }
# never ship a developer's real connection string
Remove-Item (Join-Path $pkg "app\appsettings.Local.json") -ErrorAction SilentlyContinue

Write-Host "== 2. scripts, database scripts, samples, docs" -ForegroundColor Cyan
New-Item -ItemType Directory -Force (Join-Path $pkg "database"), (Join-Path $pkg "docs") | Out-Null
Copy-Item -Recurse (Join-Path $repo "database\postgres") (Join-Path $pkg "database\postgres")
Copy-Item -Recurse (Join-Path $repo "samples") (Join-Path $pkg "samples")
foreach ($s in "run.ps1", "setup-postgres-portable.ps1", "stop-postgres.ps1") { Copy-Item (Join-Path $PSScriptRoot $s) $pkg }
foreach ($d in "TCS_LAPTOP_SETUP.md", "USER_GUIDE.md") { Copy-Item (Join-Path $repo "docs\backend\$d") (Join-Path $pkg "docs") }

$start = @'
SecureShift - OFFLINE package
=============================
No Git, no build and no internet are needed. You only need .NET 8 on the laptop:
    dotnet --list-runtimes      (must show Microsoft.NETCore.App 8.0.x)

1. Unzip this zip, for example to C:\SecureShift
   (you get C:\SecureShift\run.ps1, C:\SecureShift\app\, ...)

2. Copy the PostgreSQL zip from the pen drive NEXT TO run.ps1:
       C:\SecureShift\postgresql-16.11-1-windows-x64-binaries.zip
   (or put it in C:\SecureShiftTools). It is used as it is - nothing is downloaded.

3. Open PowerShell in C:\SecureShift and type:
       powershell -ExecutionPolicy Bypass -File run.ps1
   The first time it unzips PostgreSQL to C:\SecureShiftTools (a few minutes), starts it on port 5434
   (this computer only, no admin rights), creates the tables + sample data and opens the login screen.

4. Log in:   Super Admin   arya.mujumdar@secureshift.io        Super@2026
             Cloud Admin   aarav.mehta@secureshift.io          Cloud@2026
             Admin         sachin.kumar@secureshift.io         Admin@2026
             Architect     amanjeet.sharma@secureshift.io      Architect@2026
             Executive     shivanand.kumatekar@secureshift.io  Exec@2026

Next time:          powershell -ExecutionPolicy Bypass -File run.ps1
Stop the database:  powershell -ExecutionPolicy Bypass -File stop-postgres.ps1
Fresh sample data:  powershell -ExecutionPolicy Bypass -File run.ps1 -Reset
Analysis Wizard sample files: samples\governance\role-assignments.json, activity-log-90d.json, roles-catalog.json
(choose "4. Custom range", From 08/12/2026, To 11/09/2026)

PowerShell scripts blocked by policy? Open docs\TCS_LAPTOP_SETUP.md, path D: the same steps typed by hand.
Every menu with the exact keys to type: docs\USER_GUIDE.md
'@
Set-Content -Path (Join-Path $pkg "START-HERE.txt") -Value $start -Encoding ascii

Write-Host "== 3. zip" -ForegroundColor Cyan
Remove-Item $Output -ErrorAction SilentlyContinue
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
# entry by entry with "/" separators (ZipFile.CreateFromDirectory in Windows PowerShell 5.1 writes "\", which
# some unzip tools do not understand)
$zip = [System.IO.Compression.ZipFile]::Open($Output, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem -Path $stage -Recurse -File) {
        $entry = $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $entry, [System.IO.Compression.CompressionLevel]::Optimal)
    }
} finally {
    $zip.Dispose()
}
Remove-Item -Recurse -Force $stage
$size = (Get-Item $Output).Length
Write-Host ("[OK] {0} ({1:0.0} MB)" -f $Output, ($size / 1MB)) -ForegroundColor Green
