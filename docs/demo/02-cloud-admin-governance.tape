# SecureShift demo 02 - Cloud Admin (Governance): dashboard, Analysis Wizard with typed JSON paths
# (a bad file first, then the 3 sample files), results, identity detail, findings, generated role, report, notifications.
# Record from the repository root (Windows):  vhs docs/demo/02-cloud-admin-governance.tape
Output docs/demo/02-cloud-admin-governance.gif
Output docs/demo/02-cloud-admin-governance.mp4
Set Shell "powershell"
Set FontSize 14
Set Width 1280
Set Height 760
Set Padding 20
Set TypingSpeed 50ms
Set Framerate 12

Hide
Type "powershell -NoProfile -ExecutionPolicy Bypass -File scripts\windows\run.ps1 -Reset -SetupOnly *> $null; $env:ConnectionStrings__SecureShiftDb = Get-Content C:\SecureShiftTools\connection-string.txt; $app = 'src\SecureShift.Console\bin\Release\net8.0\SecureShift.Console.dll'; Clear-Host"
Enter
Wait@180s
Show

Type "dotnet $app"
Enter
Wait+Screen@30s /Choose:/
Sleep 1s
Type "1"
Enter
Type "aarav.mehta@secureshift.io"
Enter
Type "Cloud@2026"
Enter
Wait+Screen@20s /Logout/
Sleep 2s

# dashboard
Type "1"
Enter
Sleep 4s

# Analysis Wizard with a BAD file: friendly error, nothing saved
Type "3"
Enter
Sleep 1s
Type "1"
Enter
Type "4"
Enter
Type "08/12/2026"
Enter
Type "11/09/2026"
Enter
Sleep 1s
Type "samples\governance\bad\role-assignments-missing-fields.json"
Enter
Type "samples\governance\activity-log-90d.json"
Enter
Type "samples\governance\roles-catalog.json"
Enter
Sleep 4s

# Analysis Wizard with the 3 sample files
Type "3"
Enter
Sleep 1s
Type "1"
Enter
Type "4"
Enter
Type "08/12/2026"
Enter
Type "11/09/2026"
Enter
Sleep 1s
Type "samples\governance\role-assignments.json"
Enter
Type "samples\governance\activity-log-90d.json"
Enter
Type "samples\governance\roles-catalog.json"
Enter
Sleep 3s
Type "y"
Enter
Sleep 5s

# identities of the new analysis -> the top identity
Type "5"
Enter
Sleep 3s
Type "1"
Enter
Sleep 5s
Type "0"
Enter
Type "0"
Enter

# findings
Type "6"
Enter
Sleep 4s
Type "0"
Enter

# generated roles -> first role -> mark as reviewed
Type "7"
Enter
Sleep 2s
Type "1"
Enter
Sleep 5s
Type "1"
Enter
Sleep 2s
Type "0"
Enter
Type "0"
Enter

# report for Northwind Traders, preview
Type "10"
Enter
Sleep 1.5s
Type "G"
Enter
Type "1"
Enter
Sleep 2s
Type "1"
Enter
Sleep 4s
Type "n"
Enter
Type "0"
Enter

# notifications
Type "11"
Enter
Sleep 3s
Type "y"
Enter
Sleep 2s

Type "0"
Enter
Type "y"
Enter
Sleep 1.5s
Type "0"
Enter
Sleep 2s
