# SecureShift demo 00 - setup on a TCS laptop: only .NET 8 SDK + PowerShell, no Docker, no admin rights.
#   fresh clone -> portable PostgreSQL 16 -> connection string -> build -> db setup -> db status -> smoke -> the app
# Record from the repository root (Windows):  vhs docs/demo/00-setup-tcs-laptop.tape
# (the PostgreSQL zip is already in C:\SecureShiftTools, so the 310 MB download is skipped)
Output docs/demo/00-setup-tcs-laptop.gif
Output docs/demo/00-setup-tcs-laptop.mp4
Set Shell "powershell"
Set FontSize 14
Set Width 1280
Set Height 760
Set Padding 20
Set TypingSpeed 50ms
Set Framerate 10

# hidden: start from zero (no unzipped PostgreSQL, no data, no clone)
Hide
Type "powershell -NoProfile -ExecutionPolicy Bypass -File scripts\windows\stop-postgres.ps1 *> $null; Remove-Item -Recurse -Force C:\SecureShiftTools\pgsql, C:\SecureShiftTools\pgdata, C:\Temp\SecureShiftDemo -ErrorAction SilentlyContinue; Remove-Item C:\SecureShiftTools\*.txt, C:\SecureShiftTools\postgres.log -ErrorAction SilentlyContinue; New-Item -ItemType Directory -Force C:\Temp *> $null; Set-Location C:\Temp; Clear-Host"
Enter
Wait@120s
Show

Type "# SecureShift on a TCS laptop: only the .NET 8 SDK and PowerShell (no Docker, no admin rights)"
Enter
Type "dotnet --version"
Enter
Sleep 2s
Type "git clone https://github.com/Yashvardhandhondge/Minrole.git C:\Temp\SecureShiftDemo"
Enter
Wait@180s
Type "cd C:\Temp\SecureShiftDemo"
Enter
Sleep 1s

Type "# 1. Portable PostgreSQL 16 in C:\SecureShiftTools (port 5434, this computer only)"
Enter
Type "powershell -ExecutionPolicy Bypass -File scripts\windows\setup-postgres-portable.ps1"
Enter
Wait@400s
Sleep 3s

Type "# 2. The connection string, for this window only"
Enter
Type "$env:ConnectionStrings__SecureShiftDb = Get-Content C:\SecureShiftTools\connection-string.txt"
Enter
Sleep 1s

Type "# 3. Build (Release)"
Enter
Type "dotnet build SecureShift.sln -c Release -v q -nologo"
Enter
Wait@600s
Sleep 2s
Type "$app = 'src\SecureShift.Console\bin\Release\net8.0\SecureShift.Console.dll'"
Enter

Type "# 4. Tables + sample data, then check"
Enter
Type "dotnet $app db setup --yes"
Enter
Wait@180s
Sleep 3s
Type "dotnet $app db status"
Enter
Wait@120s
Sleep 4s
Type "dotnet $app smoke"
Enter
Wait@120s
Sleep 3s

Type "# 5. Start the app"
Enter
Type "dotnet $app"
Enter
Sleep 5s
Sleep 2s
Type "1"
Enter
Type "arya.mujumdar@secureshift.io"
Enter
Type "Super@2026"
Enter
Sleep 4s
Sleep 3s
Type "0"
Enter
Type "y"
Enter
Sleep 1.5s
Type "0"
Enter
Sleep 2s
Type "# Stop the database later with:  powershell -ExecutionPolicy Bypass -File scripts\windows\stop-postgres.ps1"
Enter
Sleep 3s
